using DrillMagic.Core;
using DrillMagic.Core.Projects;
using DrillMagic.Core.Sizing;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using DrillMagic.Windows.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT;
using WinRT.Interop;
using WinUIEx;

namespace DrillMagic.Windows;
public sealed partial class MainWindow : WindowEx, INotifyPropertyChanged
{
    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    // Private fields
    private SolidColorBrush _selectedBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0,0,0,0));
    private string _selectedColorName = "None";
    private ISharedInteractionService? _sharedInteractionService; 
    private volatile bool _suppressColorChanged = false;
    private readonly System.Threading.SemaphoreSlim _dialogLock = new(1, 1);
    private bool _isProcessingCellSizeChange = false;
    private uint? _pendingCellSize = null;
    private bool _isUpdatingDimensionsProgrammatically = false;

    // Services
    private readonly IColorMapService _colorMapService;
    private readonly IPdfGenerator _pdfGenerator;
    private readonly DrillMagic.Core.Export.IGridImageExportService _imageExportService;
    private readonly IRecentProjectsService _recentProjectsService;
    private readonly ILogger<MainWindow> _logger;

    // Public properties
    public SolidColorBrush SelectedBrush 
    { 
        get => _selectedBrush;
        set
        {
            _selectedBrush = value;
            OnPropertyChanged();
        }
    }

    public IDrillGridManager? DrillGridManager { get; private set; }

    public ObservableCollection<DMCColor> FilteredColors { get; set; } = [];

    public string SelectedColorName
    {
        get => _selectedColorName;
        set
        {
            _selectedColorName = value;
            OnPropertyChanged();
        }
    }

    public IList<Color> ToolkitPaletteColors { get; private set; } = [];

    // Constructor
    public MainWindow()
    {
        InitializeComponent(); 
        
        // Resolve services from the App's service provider
        var services = App.Current.Services!;
        _colorMapService = services.GetRequiredService<IColorMapService>();
        _pdfGenerator = services.GetRequiredService<IPdfGenerator>();
        _imageExportService = services.GetRequiredService<DrillMagic.Core.Export.IGridImageExportService>();
        _recentProjectsService = services.GetRequiredService<IRecentProjectsService>();
        _logger = services.GetRequiredService<ILogger<MainWindow>>();

        _logger.LogInformation("MainWindow initialized");

        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.Loaded += MainWindow_Loaded;
        }
    }

    // Lifecycle / initialization
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("MainWindow loaded");
        try 
        {
            await PopulateToolkitPaletteAsync();
            DrillGridManager = App.Current.Services!.GetRequiredService<IDrillGridManager>();
            DrillGridManager.PropertyChanged += DrillGridManager_PropertyChanged;
            _sharedInteractionService = App.Current.Services!.GetRequiredService<ISharedInteractionService>();
            _sharedInteractionService.PropertyChanged += InteractionServicePropertyChanged;
            
            _recentProjectsService.RecentProjectsChanged += (s, args) => DispatcherQueue.TryEnqueue(PopulateRecentProjectsMenu);
            PopulateRecentProjectsMenu();
            UpdateVersionsList();
            UpdateWindowTitle();

            // Data initialization happens in the service constructor now, 
            // so we just update the UI.
            UpdateFilteredColors();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical error during MainWindow load");
        }
    }

    // DrillGridManager property change handler
    private void DrillGridManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IDrillGridManager.SelectedGrid))
        {
            _logger.LogInformation("Grid selection changed");
            MyDrillGridView.Grid = DrillGridManager?.SelectedGrid;
            InspectorLegend.Summary = DrillGridManager?.SelectedGrid?.ColorSummary ?? [];
            if (SquareDrillRadio != null && RoundDrillRadio != null && DrillGridManager != null)
            {
                SquareDrillRadio.IsChecked = DrillGridManager.ActiveDrillShape == DrillShape.Square;
                RoundDrillRadio.IsChecked = DrillGridManager.ActiveDrillShape == DrillShape.Round;
            }
            UpdateVersionsList();
            UpdateWindowTitle();
            UpdateMinCountUI();
            SyncDimensionsBoxesFromGrid();
            OnPropertyChanged(nameof(CurrentModeText));
            OnPropertyChanged(nameof(WelcomeOverlayVisibility));
        }
        else if (e.PropertyName == nameof(IDrillGridManager.ActiveDrillShape))
        {
            if (SquareDrillRadio != null && RoundDrillRadio != null && DrillGridManager != null)
            {
                SquareDrillRadio.IsChecked = DrillGridManager.ActiveDrillShape == DrillShape.Square;
                RoundDrillRadio.IsChecked = DrillGridManager.ActiveDrillShape == DrillShape.Round;
            }
            OnPropertyChanged(nameof(DrillGridManager));
        }
        else if (e.PropertyName == nameof(IDrillGridManager.IsDirty) ||
                 e.PropertyName == nameof(IDrillGridManager.ProjectName) ||
                 e.PropertyName == nameof(IDrillGridManager.CurrentFilePath) ||
                 e.PropertyName == nameof(IDrillGridManager.HasSavedProject) ||
                 e.PropertyName == nameof(IDrillGridManager.SourceImageName))
        {
            UpdateWindowTitle();
        }
        else if (e.PropertyName == nameof(IDrillGridManager.ActiveVersion))
        {
            UpdateVersionsList();
            UpdateWindowTitle();
        }
    }

    // Interaction service property change handler
    private void InteractionServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SharedInteractionService.CurrentMode))
        {
            UpdateInteractionButtons();
            OnPropertyChanged(nameof(CurrentModeText));
        }
        else if (e.PropertyName == nameof(SharedInteractionService.InspectedColor) &&
           sender is SharedInteractionService service)
        {
            UpdateSelectedColorName(service.InspectedColor);
        }
        else if (e.PropertyName == nameof(SharedInteractionService.HighlightedColor))
        {
            ClearHighlightButton.Visibility = _sharedInteractionService.HighlightedColor is not null
                ? Visibility.Visible
                : Visibility.Collapsed;


            if (_sharedInteractionService.HighlightedColor is not null)
                UpdateSelectedColorName(_sharedInteractionService.HighlightedColor);
            
        }
    }

    // UI update helpers
    private void UpdateInteractionButtons()
    {
        PointerButton.IsChecked = _sharedInteractionService.CurrentMode == InteractionMode.Navigate;
        PaintBucketButton.IsChecked = _sharedInteractionService.CurrentMode == InteractionMode.Coloring;
        ColorInspectorButton.IsChecked = _sharedInteractionService.CurrentMode == InteractionMode.ColorInspector;
    }

    private void UpdateSelectedColorName(DMCColor color)
    {
        SelectedColorName = color.Name;

        // suppress handler while we set the control color programmatically
        _suppressColorChanged = true;
        try
        {
            ToolkitColorPicker.Color = color.UiColor;
            ToolkitSearchBox.Text = color.Name;
            SelectedBrush = new(color.UiColor);
        }
        finally
        {
            // restore after layout completes to avoid re-entrancy
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _suppressColorChanged = false);
        }
    }

    private void UpdateFilteredColors()
    {
        var searchText = SelectedColorName ?? string.Empty;
        var allColors = _colorMapService.DefaultColorMap.Values;
        var filtered = string.IsNullOrWhiteSpace(searchText)
            ? allColors
            : allColors.Where(c => c.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
                                   || c.DMCNumber.ToString().Contains(searchText, StringComparison.CurrentCultureIgnoreCase));

        // Use the H/S/L helpers in DrillMagic.Core.Utils (available as extension methods on System.Drawing.Color)
        var ordered = filtered
            .OrderBy(c => c.Color.H())                 // hue ascending
            .ThenByDescending(c => c.Color.S())        // saturation descending
            .ThenBy(c => c.Color.L())                  // lightness ascending
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase); // deterministic fallback

        FilteredColors.Clear();
        foreach (var color in ordered)
        {
            FilteredColors.Add(color);
        }
    }

    // Formatting helpers
    public string FormatCellSize(double value) => $"Cell Size: {(int)value} px";

    public string FormatGridDimensions(DrillGrid? grid) =>
        grid != null ? $"{grid.Width} × {grid.Height} Drills" : "No Grid Loaded";

    public string FormatGridColors(DrillGrid? grid) =>
        grid != null ? $"{grid.ColorSummary.Count} DMC Colors" : "";

    public string FormatPhysicalDimensions(DrillGrid? grid, DrillShape shape) =>
        grid != null ? PhysicalDimensionsCalculator.FormatDimensions(grid.Width, grid.Height, shape) : "";

    private void DrillShapeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (DrillGridManager == null) return;
        if (sender == SquareDrillRadio)
        {
            DrillGridManager.ActiveDrillShape = DrillShape.Square;
        }
        else if (sender == RoundDrillRadio)
        {
            DrillGridManager.ActiveDrillShape = DrillShape.Round;
        }
        OnPropertyChanged(nameof(DrillGridManager));
    }

    public Visibility WelcomeOverlayVisibility => DrillGridManager?.SelectedGrid == null ? Visibility.Visible : Visibility.Collapsed;

    public string CurrentModeText => _sharedInteractionService?.CurrentMode switch
    {
        InteractionMode.Navigate => "Navigate (Pan/Zoom)",
        InteractionMode.Coloring => "Coloring (Paint)",
        InteractionMode.ColorInspector => "Color Inspector (Eyedropper)",
        _ => "Navigate"
    };

    // Studio Inspector & Layout handlers
    private void ToggleInspector_Click(object sender, RoutedEventArgs e)
    {
        var isVisible = InspectorPanel.Visibility == Visibility.Visible;
        InspectorPanel.Visibility = isVisible
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (TopToggleInspectorButton != null)
        {
            TopToggleInspectorButton.IsChecked = !isVisible;
        }
    }

    private void InspectorTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender == TabPaletteBtn)
        {
            TabPaletteBtn.IsChecked = true;
            TabGridBtn.IsChecked = false;
            TabVersionsBtn.IsChecked = false;
            PaletteTabContent.Visibility = Visibility.Visible;
            GridTabContent.Visibility = Visibility.Collapsed;
            VersionsTabContent.Visibility = Visibility.Collapsed;
        }
        else if (sender == TabGridBtn)
        {
            TabPaletteBtn.IsChecked = false;
            TabGridBtn.IsChecked = true;
            TabVersionsBtn.IsChecked = false;
            PaletteTabContent.Visibility = Visibility.Collapsed;
            GridTabContent.Visibility = Visibility.Visible;
            VersionsTabContent.Visibility = Visibility.Collapsed;
        }
        else if (sender == TabVersionsBtn)
        {
            TabPaletteBtn.IsChecked = false;
            TabGridBtn.IsChecked = false;
            TabVersionsBtn.IsChecked = true;
            PaletteTabContent.Visibility = Visibility.Collapsed;
            GridTabContent.Visibility = Visibility.Collapsed;
            VersionsTabContent.Visibility = Visibility.Visible;
        }
    }

    private void SymbolSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch && MyDrillGridView != null)
        {
            MyDrillGridView.IsSymbolOverlayEnabled = toggleSwitch.IsOn;
            SymbolOverlayButton.IsChecked = toggleSwitch.IsOn;
        }
    }

    private void WelcomeRecentButton_Click(object sender, RoutedEventArgs e)
    {
        WelcomeRecentFlyout.Items.Clear();
        var recents = _recentProjectsService.GetRecentProjects();
        if (recents.Count == 0)
        {
            WelcomeRecentFlyout.Items.Add(new MenuFlyoutItem { Text = "(No recent projects)", IsEnabled = false });
            return;
        }

        foreach (var path in recents)
        {
            var item = new MenuFlyoutItem
            {
                Text = Path.GetFileName(path),
                Tag = path
            };
            item.Click += RecentProjectItem_Click;
            WelcomeRecentFlyout.Items.Add(item);
        }
    }

    // Drag and Drop handlers
    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Drop to open in DrillMagic";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsContentVisible = true;
    }

    private async void RootGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                if (string.Equals(file.FileType, ".drillmagic", StringComparison.OrdinalIgnoreCase))
                {
                    await OpenProjectFromPathAsync(file.Path);
                }
                else if (file.FileType.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                         file.FileType.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                         file.FileType.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                         file.FileType.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
                {
                    var stream = await GetStreamFromFileAsync(file);
                    if (stream != null)
                    {
                        await OpenAndDoctorImageStreamAsync(stream, file.Name);
                    }
                }
            }
        }
    }

    // Toolkit UI handlers
    private async void ToolkitColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs e)
    {
        if (_suppressColorChanged) return;

        var picked = e.NewColor;
        _logger.LogTrace("ColorPicker changed to {Color}", picked);

        var exactMatches = _colorMapService.DefaultColorMap.Values
            .Where(d => d.UiColor.A == picked.A && d.UiColor.R == picked.R && d.UiColor.G == picked.G && d.UiColor.B == picked.B)
            .ToList();

        DMCColor? chosen = null;

        if (exactMatches.Count == 1)
        {
            chosen = exactMatches[0];
        }
        else if (exactMatches.Count > 1)
        {
            var list = new ListView
            {
                ItemsSource = exactMatches.Select(x => $"{x.DMCNumber}  {x.Name}"),
                SelectionMode = ListViewSelectionMode.Single,
                Height = 200
            };

            var dialog = new ContentDialog
            {
                Title = "Multiple matching DMC colors",
                Content = list,
                PrimaryButtonText = "Select",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && list.SelectedIndex >= 0)
                chosen = exactMatches[list.SelectedIndex];
        }
        else
        {
            chosen = _colorMapService.DefaultColorMap.Values
                .OrderBy(d =>
                {
                    var r = d.UiColor.R - picked.R;
                    var g = d.UiColor.G - picked.G;
                    var b = d.UiColor.B - picked.B;
                    return r * r + g * g + b * b;
                })
                .FirstOrDefault();
        }

        if (chosen is not null)
        {
            SelectedColorName = string.IsNullOrWhiteSpace(chosen.Name) ? chosen.DMCNumber.ToString() : chosen.Name;
            ToolkitSearchBox.Text = SelectedColorName;

            if (_sharedInteractionService is not null)
                _sharedInteractionService.SelectedColor = chosen.Color;

            SelectedBrush.Color = chosen.UiColor;

        }
    }

    private async void ToolkitSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSelectedColorName(_colorMapService.GetDMCColor(ToolkitSearchBox.Text));
    }

    private async void ToolkitPickerButton_Click(object sender, RoutedEventArgs e)
    {
        // Prevent reacting to control changes while flyout opens
        _suppressColorChanged = true;

        try
        {
            ToolkitColorFlyout.ShowAt(ToolkitPickerButton);
        }
        finally
        {
            // Re-enable after layout work completes (low priority so it runs after measure/arrange)
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                _suppressColorChanged = false;
            });
        }
    }

    // Palette population and helpers
    private static (double h, double s, double v) RgbToHsv(byte r, byte g, byte b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double d = max - min;

        double h = 0;
        if (d > 0.00001)
        {
            if (max == rd) h = (gd - bd) / d;
            else if (max == gd) h = 2 + (bd - rd) / d;
            else h = 4 + (rd - gd) / d;
            h *= 60;
            if (h < 0) h += 360;
        }
        double s = max <= 0 ? 0 : d / max;
        double v = max;
        return (h, s, v);
    }

    private async Task PopulateToolkitPaletteAsync(string? filter = null)
    {
        // Build the list off the UI thread
        List<Color> colors = await Task.Run(() =>
        {
            var entries = _colorMapService.DefaultColorMap.Values
                .Where(c => string.IsNullOrWhiteSpace(filter) ||
                            (c.Name ?? string.Empty).Contains(filter.Trim(), StringComparison.CurrentCultureIgnoreCase) ||
                            c.DMCNumber.ToString().Contains(filter?.Trim() ?? string.Empty))
                .OrderBy(c =>
                {
                    var (h, s, v) = RgbToHsv(c.UiColor.R, c.UiColor.G, c.UiColor.B);
                    return (h, 1 - s, 1 - v);
                })
                .ToList();

            return entries.Select(e => e.UiColor).ToList();
        }).ConfigureAwait(false);

        // Marshal update to UI thread at low priority so it runs after measure/arrange
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                _suppressColorChanged = true; // prevent reaction while we mutate the palette
                ToolkitColorPicker.CustomPaletteColors.Clear();
                foreach (var c in colors)
                    ToolkitColorPicker.CustomPaletteColors.Add(c);
            }
            finally
            {
                _suppressColorChanged = false;
                tcs.TrySetResult(true);
            }
        });

        await tcs.Task.ConfigureAwait(false);
    }

    // File operations
    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("OpenFile_Click invoked");
        if (DrillGridManager?.IsDirty == true)
        {
            var proceed = await PromptToDiscardOrSaveAsync();
            if (!proceed) return;
        }

        var fileOpenPicker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        fileOpenPicker.FileTypeFilter.Add(".drillmagic");
        fileOpenPicker.FileTypeFilter.Add(".jpg");
        fileOpenPicker.FileTypeFilter.Add(".jpeg");
        fileOpenPicker.FileTypeFilter.Add(".png");
        fileOpenPicker.FileTypeFilter.Add(".bmp");

        InitializeWithWindow.Initialize(fileOpenPicker, WindowNative.GetWindowHandle(this));

        var file = await fileOpenPicker.PickSingleFileAsync();
        if (file != null)
        {
            _logger.LogInformation("File picked: {FilePath}", file.Path);
            if (string.Equals(file.FileType, ".drillmagic", StringComparison.OrdinalIgnoreCase))
            {
                await OpenProjectFromPathAsync(file.Path);
            }
            else
            {
                var imageStream = await GetStreamFromFileAsync(file);
                if (imageStream is null) return;
                await OpenAndDoctorImageStreamAsync(imageStream, file.Name);
            }
        }
        else
        {
            _logger.LogInformation("File picker cancelled");
        }
    }

    private async Task OpenAndDoctorImageStreamAsync(MemoryStream imageStream, string? suggestedName = null)
    {
        if (DrillGridManager is null) return;
        if (!await _dialogLock.WaitAsync(0))
        {
            _logger.LogWarning("Suppressed concurrent ImageDoctorDialog attempt");
            return;
        }

        try
        {
            var (apply, resultStream) = await Controls.ImageDoctorDialog.ShowForImageAsync(
                imageStream,
                this.Content.XamlRoot,
                DrillGridManager.ActiveDrillShape);

            if (!apply)
            {
                _logger.LogInformation("Image doctor cancelled by user");
                return;
            }

            if (resultStream != null)
            {
                imageStream.Position = 0;
                await DrillGridManager.LoadImageAsync(imageStream, suggestedName);

                resultStream.Position = 0;
                await DrillGridManager.ApplyDoctoredImageAsync(resultStream, "Initial Doctored Image");
            }
            else
            {
                imageStream.Position = 0;
                await DrillGridManager.LoadImageAsync(imageStream, suggestedName);
            }

            if (DrillGridManager.AvailableCellSizes.Any())
            {
                CellSizeSlider.Minimum = DrillGridManager.AvailableCellSizes.Min();
                CellSizeSlider.Maximum = DrillGridManager.AvailableCellSizes.Max();
                var defaultIdx = Math.Min(9, DrillGridManager.AvailableCellSizes.Count - 1);
                CellSizeSlider.Value = DrillGridManager.AvailableCellSizes[defaultIdx];
                CellSizePanel.Visibility = Visibility.Visible;
                CellSizeSeparator.Visibility = Visibility.Visible;
            }
            else
            {
                CellSizePanel.Visibility = Visibility.Collapsed;
                CellSizeSeparator.Visibility = Visibility.Collapsed;
            }

            if (DrillGridManager.SelectedGrid != null)
            {
                MyDrillGridView.Grid = DrillGridManager.SelectedGrid;
            }

            UpdateVersionsList();
            UpdateWindowTitle();
            OnPropertyChanged(nameof(DrillGridManager));
            OnPropertyChanged(nameof(WelcomeOverlayVisibility));
        }
        finally
        {
            _dialogLock.Release();
        }
    }

    private async Task DoctorExistingImageAsync(MemoryStream imageStream, string? suggestedName = null)
    {
        if (DrillGridManager is null) return;
        if (!await _dialogLock.WaitAsync(0))
        {
            _logger.LogWarning("Suppressed concurrent ImageDoctorDialog attempt");
            return;
        }

        try
        {
            var (apply, resultStream) = await Controls.ImageDoctorDialog.ShowForImageAsync(
                imageStream,
                this.Content.XamlRoot,
                DrillGridManager.ActiveDrillShape,
                DrillGridManager.SourceImageHistory,
                DrillGridManager.CurrentProject?.SourceImageVersionsData);

            if (!apply || resultStream == null)
            {
                _logger.LogInformation("Image doctor cancelled or no edits made");
                return;
            }

            resultStream.Position = 0;
            string versionName = suggestedName ?? $"Doctored Image {DrillGridManager.SourceImageHistory.Count + 1}";
            await DrillGridManager.ApplyDoctoredImageAsync(resultStream, versionName);

            if (DrillGridManager.AvailableCellSizes.Any())
            {
                CellSizeSlider.Minimum = DrillGridManager.AvailableCellSizes.Min();
                CellSizeSlider.Maximum = DrillGridManager.AvailableCellSizes.Max();
                if (DrillGridManager.SelectedGrid?.CellSize > 0)
                {
                    CellSizeSlider.Value = DrillGridManager.SelectedGrid.CellSize;
                }
                CellSizePanel.Visibility = Visibility.Visible;
                CellSizeSeparator.Visibility = Visibility.Visible;
            }

            if (DrillGridManager.SelectedGrid != null)
            {
                MyDrillGridView.Grid = DrillGridManager.SelectedGrid;
            }

            UpdateVersionsList();
            UpdateWindowTitle();
            OnPropertyChanged(nameof(DrillGridManager));
        }
        finally
        {
            _dialogLock.Release();
        }
    }

    private async void EditSourceImageButton_Click(object sender, RoutedEventArgs e)
    {
        if (DrillGridManager?.SourceImageBytes is null || DrillGridManager.SourceImageBytes.Length == 0)
        {
            await ShowErrorDialog("No Source Image", "The current project does not contain a source image to edit.");
            return;
        }

        if (DrillGridManager.IsDirty)
        {
            var dialog = new ContentDialog
            {
                Title = "Edit Source Image?",
                Content = "Doctoring the source image and re-generating will discard any unsaved grid modifications. Do you want to proceed?",
                PrimaryButtonText = "Continue",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };
            var res = await ShowDialogSafelyAsync(dialog);
            if (res != ContentDialogResult.Primary) return;
        }

        var sourceStream = new MemoryStream(DrillGridManager.SourceImageBytes);
        await DoctorExistingImageAsync(sourceStream);
    }

    private void SubTabVersions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton clicked)
        {
            SubTabGridsBtn.IsChecked = clicked == SubTabGridsBtn;
            SubTabBaseImagesBtn.IsChecked = clicked == SubTabBaseImagesBtn;

            GridVersionsPanel.Visibility = SubTabGridsBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            BaseImageVersionsPanel.Visibility = SubTabBaseImagesBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async void ResliceToImageVersion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string versionId } && DrillGridManager != null)
        {
            if (DrillGridManager.IsDirty)
            {
                var dialog = new ContentDialog
                {
                    Title = "Re-slice from Image Version?",
                    Content = "Re-slicing will regenerate the grid from this base image iteration and discard unsaved modifications to the current grid. Do you wish to proceed?",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.Content.XamlRoot
                };
                var res = await ShowDialogSafelyAsync(dialog);
                if (res != ContentDialogResult.Primary) return;
            }

            try
            {
                await DrillGridManager.SwitchToSourceImageVersionAsync(versionId);
                if (DrillGridManager.SelectedGrid != null)
                {
                    MyDrillGridView.Grid = DrillGridManager.SelectedGrid;
                }
                UpdateVersionsList();
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch source image version");
                await ShowErrorDialog("Switch Version Failed", ex.Message);
            }
        }
    }

    private async void DoctorImageVersion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string versionId } && DrillGridManager?.CurrentProject != null)
        {
            if (DrillGridManager.CurrentProject.SourceImageVersionsData.TryGetValue(versionId, out var imgBytes) && imgBytes.Length > 0)
            {
                var summary = DrillGridManager.SourceImageHistory.FirstOrDefault(s => s.Id == versionId);
                string suggestedName = summary != null ? $"Edits of {summary.Name}" : "Doctored Image";
                var stream = new MemoryStream(imgBytes);
                await DoctorExistingImageAsync(stream, suggestedName);
            }
        }
    }

    private async Task OpenProjectFromPathAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            await DrillGridManager!.LoadProjectAsync(stream, path);
            _recentProjectsService.AddRecentProject(path);

            if (DrillGridManager.AvailableCellSizes.Any())
            {
                CellSizeSlider.Minimum = DrillGridManager.AvailableCellSizes.Min();
                CellSizeSlider.Maximum = DrillGridManager.AvailableCellSizes.Max();
                if (DrillGridManager.SelectedGrid?.CellSize > 0)
                {
                    CellSizeSlider.Value = DrillGridManager.SelectedGrid.CellSize;
                }
                CellSizePanel.Visibility = Visibility.Visible;
                CellSizeSeparator.Visibility = Visibility.Visible;
            }
            else
            {
                CellSizePanel.Visibility = Visibility.Collapsed;
                CellSizeSeparator.Visibility = Visibility.Collapsed;
            }

            UpdateVersionsList();
            UpdateWindowTitle();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project from {Path}", path);
            await ShowErrorDialog("Failed to Open Project", $"Could not load .drillmagic project.\n\nDetails: {ex.Message}");
        }
    }

    private async Task<bool> PromptToDiscardOrSaveAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Unsaved Changes",
            Content = "You have unsaved changes in the current project. Do you want to save your changes before proceeding?",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Don't Save",
            CloseButtonText = "Cancel",
            XamlRoot = this.Content.XamlRoot
        };

        var result = await ShowDialogSafelyAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            return await SaveProjectInternalAsync();
        }
        else if (result == ContentDialogResult.Secondary)
        {
            return true; // Discard changes
        }
        return false; // Cancel
    }

    private void PopulateRecentProjectsMenu()
    {
        RecentProjectsMenu.Items.Clear();
        var recents = _recentProjectsService.GetRecentProjects();
        if (recents.Count == 0)
        {
            var emptyItem = new MenuFlyoutItem { Text = "(No recent projects)", IsEnabled = false };
            RecentProjectsMenu.Items.Add(emptyItem);
            return;
        }

        foreach (var path in recents)
        {
            var item = new MenuFlyoutItem
            {
                Text = Path.GetFileName(path),
                Tag = path
            };
            item.Click += RecentProjectItem_Click;
            RecentProjectsMenu.Items.Add(item);
        }
    }

    private async void RecentProjectItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string path)
        {
            if (File.Exists(path))
            {
                await OpenProjectFromPathAsync(path);
            }
            else
            {
                _recentProjectsService.RemoveRecentProject(path);
                await ShowErrorDialog("File Not Found", $"The project file could not be found:\n{path}");
            }
        }
    }

    private void UpdateWindowTitle()
    {
        if (DrillGridManager == null)
        {
            this.Title = "DrillMagic";
            return;
        }

        string title;
        if (DrillGridManager.HasSavedProject)
        {
            // The project is saved / open -> show the project title in the title bar!
            title = DrillGridManager.ProjectName;
        }
        else if (!string.IsNullOrWhiteSpace(DrillGridManager.SourceImageName))
        {
            // No project has been created yet -> show the source image!
            title = Path.GetFileName(DrillGridManager.SourceImageName);
        }
        else if (!string.IsNullOrWhiteSpace(DrillGridManager.ProjectName) && DrillGridManager.ProjectName != "Untitled Project")
        {
            title = DrillGridManager.ProjectName;
        }
        else if (DrillGridManager.SelectedGrid != null)
        {
            title = "Untitled Project";
        }
        else
        {
            this.Title = "DrillMagic";
            return;
        }

        string dirty = DrillGridManager.IsDirty ? " *" : "";
        string version = DrillGridManager.ActiveVersion != null ? $" [{DrillGridManager.ActiveVersion.Name}]" : "";
        this.Title = $"DrillMagic - {title}{version}{dirty}";
    }

    private string GetDefaultProjectFileName()
    {
        if (DrillGridManager != null)
        {
            if (!string.IsNullOrWhiteSpace(DrillGridManager.ProjectName) && DrillGridManager.ProjectName != "Untitled Project")
            {
                return SanitizeFileName(DrillGridManager.ProjectName);
            }
            if (!string.IsNullOrWhiteSpace(DrillGridManager.SourceImageName))
            {
                return SanitizeFileName(Path.GetFileNameWithoutExtension(DrillGridManager.SourceImageName));
            }
        }
        return "DrillMagicProject";
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "DrillMagic";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "DrillMagic" : result;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveProjectInternalAsync();
    }

    private async void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveProjectAsInternalAsync();
    }

    private async Task<bool> SaveProjectInternalAsync()
    {
        if (DrillGridManager?.SelectedGrid == null)
        {
            await ShowErrorDialog("Nothing to Save", "Please open or create a grid first.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(DrillGridManager.CurrentFilePath))
        {
            try
            {
                await DrillGridManager.SaveProjectAsync(DrillGridManager.CurrentFilePath);
                _recentProjectsService.AddRecentProject(DrillGridManager.CurrentFilePath);
                UpdateWindowTitle();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save project to {Path}", DrillGridManager.CurrentFilePath);
                await ShowErrorDialog("Save Failed", $"An error occurred while saving the project: {ex.Message}");
                return false;
            }
        }
        else
        {
            return await SaveProjectAsInternalAsync();
        }
    }

    private async Task<bool> SaveProjectAsInternalAsync()
    {
        if (DrillGridManager?.SelectedGrid == null)
        {
            await ShowErrorDialog("Nothing to Save", "Please open or create a grid first.");
            return false;
        }

        var savePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = GetDefaultProjectFileName()
        };
        savePicker.FileTypeChoices.Add("DrillMagic Project", new[] { ".drillmagic" });

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(savePicker, hwnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file is null) return false;

        try
        {
            await DrillGridManager.SaveProjectAsync(file.Path);
            _recentProjectsService.AddRecentProject(file.Path);
            UpdateWindowTitle();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during project save as");
            await ShowErrorDialog("Failed to Save Project", $"An error occurred while saving the project: {ex.Message}");
            return false;
        }
    }

    private async void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        if (DrillGridManager?.SelectedGrid is null)
        {
            await ShowErrorDialog("Nothing to Export", "Please open an image or project before exporting.");
            return;
        }

        string baseName = GetDefaultProjectFileName();
        var savePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = $"{baseName}_Export"
        };
        savePicker.FileTypeChoices.Add("PNG Image", new[] { ".png" });

        InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(this));
        var file = await savePicker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            var options = new DrillMagic.Core.Export.ImageExportOptions
            {
                PixelsPerCell = 20,
                IncludeGridLines = true
            };
            var pngBytes = await _imageExportService.ExportToPngAsync(DrillGridManager.SelectedGrid, options);
            await File.WriteAllBytesAsync(file.Path, pngBytes);
            _logger.LogInformation("Exported PNG successfully to {Path}", file.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export PNG");
            await ShowErrorDialog("PNG Export Failed", $"Could not export the grid to PNG.\n\nDetails: {ex.Message}");
        }
    }

    private void UpdateVersionsList()
    {
        if (DrillGridManager == null) return;
        VersionsListView.ItemsSource = null;
        VersionsListView.ItemsSource = DrillGridManager.Versions;
        VersionsListView.SelectedItem = DrillGridManager.ActiveVersion;

        BaseImageVersionsListView.ItemsSource = null;
        BaseImageVersionsListView.ItemsSource = DrillGridManager.SourceImageHistory;
        BaseImageVersionsListView.SelectedItem = DrillGridManager.ActiveSourceImageVersion;
    }

    private async void CreateVersionSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (DrillGridManager?.SelectedGrid == null)
        {
            await ShowErrorDialog("Cannot Snapshot", "No active grid to snapshot.");
            return;
        }

        var inputTextBox = new TextBox
        {
            Text = $"Iteration {DrillGridManager.Versions.Count + 1}",
            PlaceholderText = "Enter version name"
        };

        var dialog = new ContentDialog
        {
            Title = "Snapshot Current Configuration",
            Content = inputTextBox,
            PrimaryButtonText = "Save Version",
            CloseButtonText = "Cancel",
            XamlRoot = this.Content.XamlRoot
        };

        var result = await ShowDialogSafelyAsync(dialog);
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputTextBox.Text))
        {
            await DrillGridManager.CreateVersionSnapshotAsync(inputTextBox.Text.Trim());
            UpdateVersionsList();
            UpdateWindowTitle();
        }
    }

    private async void VersionsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VersionsListView.SelectedItem is ProjectVersionSummary selected &&
            selected != DrillGridManager?.ActiveVersion)
        {
            if (DrillGridManager?.IsDirty == true)
            {
                var proceed = await PromptToDiscardOrSaveAsync();
                if (!proceed)
                {
                    VersionsListView.SelectedItem = DrillGridManager.ActiveVersion;
                    return;
                }
            }

            try
            {
                await DrillGridManager!.SwitchToVersionAsync(selected.Id);
                UpdateVersionsList();
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to switch version to {VersionId}", selected.Id);
                await ShowErrorDialog("Version Switch Failed", $"Could not load version {selected.Name}: {ex.Message}");
            }
        }
    }

    private void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = SaveProjectInternalAsync();
    }

    private void SaveAsAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = SaveProjectAsInternalAsync();
    }

    private void OpenAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OpenFile_Click(this, new RoutedEventArgs());
    }

    private async Task<MemoryStream?> GetStreamFromFileAsync(StorageFile file)
    {
        try
        {
            var stream = await file.OpenReadAsync();
            var memoryStream = new MemoryStream();
            await stream.AsStreamForRead().CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file {FilePath}", file.Path);
            await ShowErrorDialog("Error Reading File", $"Could not read the selected file. Please ensure it is accessible and not corrupted.\n\nDetails: {ex.Message}");
            return null;
        }
    }

    private async void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("PrintButton_Click invoked");
        if (MyDrillGridView.Grid is null)
        {
             _logger.LogWarning("Print attempted with no grid loaded");
            return;
        }

        string baseName = GetDefaultProjectFileName();
        var savePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = $"{baseName}_Pattern"
        };
        savePicker.FileTypeChoices.Add("PDF Document", new[] { ".pdf" });

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(savePicker, hwnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file is null) return;

        try
        {
            // Call the new service-based generator.
            var (pdfBytes, errorMessage) = await _pdfGenerator.GenerateDrillGridPdf(MyDrillGridView.Grid);

            if (pdfBytes is null || pdfBytes.Length == 0)
            {
                string errorContent = "The PDF generator failed to create a document.";
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    errorContent += $"\n\nDetails:\n{errorMessage}";
                }
                _logger.LogError("PDF generation failed: {ErrorMessage}", errorMessage);
                await ShowErrorDialog("PDF Generation Failed", errorContent);
                return;
            }

            await File.WriteAllBytesAsync(file.Path, pdfBytes);
            _logger.LogInformation("PDF saved to {Path}", file.Path);
            
            // Optional: You could show a "Success" dialog or open the file automatically.
            // await ShowErrorDialog("Success", "PDF saved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during PDF save");
            await ShowErrorDialog("Failed to Save PDF", $"An error occurred while saving the file: {ex.Message}");
        }
    }

    private async Task ShowErrorDialog(string title, string content)
    {
        _logger.LogError("Showing error dialog: {Title} - {Content}", title, content);
        // Use a TextBlock for robust multi-line display.
        var textBlock = new TextBlock
        {
            Text = content,
            IsTextSelectionEnabled = true, // Make the text copyable.
            TextWrapping = TextWrapping.Wrap
        };

        var scrollViewer = new ScrollViewer
        {
            Content = textBlock,
            MaxHeight = 250
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = scrollViewer,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };
        await ShowDialogSafelyAsync(dialog);
    }

    /// <summary>
    /// Displays a ContentDialog safely, preventing COMException 0x80000019 (Only a single ContentDialog can be open at any time).
    /// </summary>
    private async Task<ContentDialogResult> ShowDialogSafelyAsync(ContentDialog dialog)
    {
        if (!await _dialogLock.WaitAsync(0))
        {
            _logger.LogWarning("Suppressed concurrent ContentDialog attempt: {Title}", dialog.Title);
            return ContentDialogResult.None;
        }

        try
        {
            return await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying ContentDialog: {Title}", dialog.Title);
            return ContentDialogResult.None;
        }
        finally
        {
            _dialogLock.Release();
        }
    }

    // Interaction / mode controls
    private void PointerButton_Click(object sender, RoutedEventArgs e)
    {
        _sharedInteractionService.CurrentMode = InteractionMode.Navigate;
    }

    private void PaintBucketButton_Click(object sender, RoutedEventArgs e)
    {
        _sharedInteractionService.CurrentMode = InteractionMode.Coloring;
    }

    private void ColorInspectorButton_Click(object sender, RoutedEventArgs e)
    {
        _sharedInteractionService.CurrentMode = InteractionMode.ColorInspector;
    }

    // Grid / view controls
    private async void CellSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (DrillGridManager is null) return;

        uint newSize = (uint)Math.Round(e.NewValue);

        // If currently processing an active cell size change or showing a dialog, buffer the latest value and return
        if (_isProcessingCellSizeChange)
        {
            _pendingCellSize = newSize;
            return;
        }

        if (DrillGridManager.SelectedGrid?.CellSize == newSize) return;

        _isProcessingCellSizeChange = true;
        try
        {
            if (DrillGridManager.IsDirty)
            {
                var dialog = new ContentDialog
                {
                    Title = "Re-slice from Image?",
                    Content = "Changing cell size will regenerate the grid from the source image and discard any unsaved modifications to the current grid. Do you wish to continue?",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.Content.XamlRoot
                };
                var res = await ShowDialogSafelyAsync(dialog);
                if (res != ContentDialogResult.Primary)
                {
                    _pendingCellSize = null;
                    CellSizeSlider.Value = DrillGridManager.SelectedGrid?.CellSize ?? e.OldValue;
                    return;
                }
            }

            var targetSize = _pendingCellSize ?? newSize;
            _pendingCellSize = null;
            await DrillGridManager.SelectGridAsync(targetSize);

            // Process any additional slider changes that arrived while slicing
            while (_pendingCellSize.HasValue)
            {
                var queuedSize = _pendingCellSize.Value;
                _pendingCellSize = null;
                if (DrillGridManager.SelectedGrid?.CellSize != queuedSize)
                {
                    await DrillGridManager.SelectGridAsync(queuedSize);
                }
            }

            UpdateVersionsList();
            UpdateWindowTitle();
            SyncDimensionsBoxesFromGrid();
            OnPropertyChanged(nameof(DrillGridManager));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing cell size to {Size}", newSize);
        }
        finally
        {
            _isProcessingCellSizeChange = false;
        }
    }

    private void ScalingMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender == ModeCellSizeBtn)
        {
            ModeCellSizeBtn.IsChecked = true;
            ModeGridCountBtn.IsChecked = false;
            CellSizeScalingPanel.Visibility = Visibility.Visible;
            GridCountScalingPanel.Visibility = Visibility.Collapsed;
        }
        else if (sender == ModeGridCountBtn)
        {
            ModeCellSizeBtn.IsChecked = false;
            ModeGridCountBtn.IsChecked = true;
            CellSizeScalingPanel.Visibility = Visibility.Collapsed;
            GridCountScalingPanel.Visibility = Visibility.Visible;

            SyncDimensionsBoxesFromGrid();
        }
    }

    private void SyncDimensionsBoxesFromGrid()
    {
        if (TargetWidthBox == null || TargetHeightBox == null || DrillGridManager?.SelectedGrid == null)
            return;

        _isUpdatingDimensionsProgrammatically = true;
        TargetWidthBox.Value = DrillGridManager.SelectedGrid.Width;
        TargetHeightBox.Value = DrillGridManager.SelectedGrid.Height;
        _isUpdatingDimensionsProgrammatically = false;
        UpdateShavingAlert();
    }

    private void TargetDimensions_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingDimensionsProgrammatically) return;

        if (LockAspectRatioCheck?.IsChecked == true && DrillGridManager?.SourceImageWidth > 0 && DrillGridManager?.SourceImageHeight > 0)
        {
            if (sender == TargetWidthBox)
            {
                int newW = (int)Math.Round(args.NewValue);
                if (newW > 0)
                {
                    _isUpdatingDimensionsProgrammatically = true;
                    int calculatedH = (int)Math.Max(1, Math.Round((double)newW * DrillGridManager.SourceImageHeight / DrillGridManager.SourceImageWidth));
                    TargetHeightBox.Value = calculatedH;
                    _isUpdatingDimensionsProgrammatically = false;
                }
            }
            else if (sender == TargetHeightBox)
            {
                int newH = (int)Math.Round(args.NewValue);
                if (newH > 0)
                {
                    _isUpdatingDimensionsProgrammatically = true;
                    int calculatedW = (int)Math.Max(1, Math.Round((double)newH * DrillGridManager.SourceImageWidth / DrillGridManager.SourceImageHeight));
                    TargetWidthBox.Value = calculatedW;
                    _isUpdatingDimensionsProgrammatically = false;
                }
            }
        }

        UpdateShavingAlert();
    }

    private void LockAspectRatio_Checked(object sender, RoutedEventArgs e)
    {
        if (DrillGridManager?.SourceImageWidth > 0 && DrillGridManager?.SourceImageHeight > 0 && TargetWidthBox != null && TargetHeightBox != null)
        {
            int currentW = (int)Math.Round(TargetWidthBox.Value);
            if (currentW > 0)
            {
                _isUpdatingDimensionsProgrammatically = true;
                TargetHeightBox.Value = (int)Math.Max(1, Math.Round((double)currentW * DrillGridManager.SourceImageHeight / DrillGridManager.SourceImageWidth));
                _isUpdatingDimensionsProgrammatically = false;
            }
        }
        UpdateShavingAlert();
    }

    private void LockAspectRatio_Unchecked(object sender, RoutedEventArgs e)
    {
        UpdateShavingAlert();
    }

    private void UpdateShavingAlert()
    {
        if (ShavingAlertText == null || ShavingAlertBorder == null || ApplyDimensionsButton == null || TargetWidthBox == null || TargetHeightBox == null)
            return;

        if (DrillGridManager == null || !DrillGridManager.HasSourceImage || DrillGridManager.SourceImageWidth <= 0 || DrillGridManager.SourceImageHeight <= 0)
        {
            ShavingAlertText.Text = "Load an image to scale grid.";
            ApplyDimensionsButton.IsEnabled = false;
            return;
        }

        int targetW = (int)Math.Round(TargetWidthBox.Value);
        int targetH = (int)Math.Round(TargetHeightBox.Value);

        if (targetW <= 0 || targetH <= 0)
        {
            ShavingAlertText.Text = "Width and Height must be at least 1 drill.";
            ApplyDimensionsButton.IsEnabled = false;
            return;
        }

        var analysis = DrillGridManager.AnalyzeTargetDimensions(targetW, targetH);
        if (analysis == null)
        {
            ShavingAlertText.Text = "Unable to analyze dimensions.";
            ApplyDimensionsButton.IsEnabled = false;
            return;
        }

        ShavingAlertText.Text = analysis.FormatAlertMessage();

        if (analysis.IsExceedingResolution || analysis.EffectiveCellSize == 0)
        {
            ApplyDimensionsButton.IsEnabled = false;
            if (ShavingAlertIcon != null)
            {
                ShavingAlertIcon.Glyph = "\uE7BA"; // Warning
                ShavingAlertIcon.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            }
        }
        else
        {
            ApplyDimensionsButton.IsEnabled = true;
            if (ShavingAlertIcon != null)
            {
                if (analysis.HasShaving)
                {
                    ShavingAlertIcon.Glyph = "\uE7BA"; // Caution
                    ShavingAlertIcon.Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
                }
                else
                {
                    ShavingAlertIcon.Glyph = "\uE73E"; // Checkmark
                    ShavingAlertIcon.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                }
            }
        }
    }

    private async void ApplyDimensionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (DrillGridManager?.SourceImageBytes == null)
        {
            await ShowErrorDialog("No Image Loaded", "Please load an image before applying grid dimensions.");
            return;
        }

        int targetW = (int)Math.Round(TargetWidthBox.Value);
        int targetH = (int)Math.Round(TargetHeightBox.Value);

        var analysis = DrillGridManager.AnalyzeTargetDimensions(targetW, targetH);
        if (analysis == null || analysis.IsExceedingResolution || analysis.EffectiveCellSize == 0)
        {
            await ShowErrorDialog("Invalid Dimensions", analysis?.FormatAlertMessage() ?? "The requested dimensions exceed image bounds.");
            return;
        }

        if (DrillGridManager.IsDirty)
        {
            var dialog = new ContentDialog
            {
                Title = "Re-slice from Image?",
                Content = "Changing grid dimensions will regenerate the grid from the source image and discard any unsaved modifications to the current grid. Do you wish to continue?",
                PrimaryButtonText = "Continue",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };
            var res = await ShowDialogSafelyAsync(dialog);
            if (res != ContentDialogResult.Primary)
            {
                return;
            }
        }

        try
        {
            await DrillGridManager.SelectGridByDimensionsAsync(targetW, targetH);

            // Overwrite cell size slider to reflect effective cell size
            _isProcessingCellSizeChange = true;
            if (CellSizeSlider != null && analysis.EffectiveCellSize >= CellSizeSlider.Minimum && analysis.EffectiveCellSize <= CellSizeSlider.Maximum)
            {
                CellSizeSlider.Value = analysis.EffectiveCellSize;
            }
            _isProcessingCellSizeChange = false;

            UpdateVersionsList();
            UpdateWindowTitle();
            UpdateShavingAlert();
            OnPropertyChanged(nameof(DrillGridManager));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply grid dimensions {W}x{H}", targetW, targetH);
            await ShowErrorDialog("Error Applying Dimensions", ex.Message);
        }
    }

    private void SymbolOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (MyDrillGridView != null)
        {
            var isChecked = SymbolOverlayButton.IsChecked ?? false;
            MyDrillGridView.IsSymbolOverlayEnabled = isChecked;
            if (InspectorSymbolSwitch != null)
            {
                InspectorSymbolSwitch.IsOn = isChecked;
            }
        }
    }

    private async void ReduceColors_Click(object sender, RoutedEventArgs e)
    {
        if (DrillGridManager.SelectedGrid == null)
        {
            return;
        }

        var numberBox = new NumberBox
        {
            Header = "Enter the target number of colors:",
            Value = DrillGridManager.SelectedGrid.ColorSummary.Count,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };

        var dialog = new ContentDialog
        {
            Title = "Reduce Colors",
            Content = numberBox,
            PrimaryButtonText = "Reduce",
            CloseButtonText = "Cancel",
            XamlRoot = this.Content.XamlRoot
        };

        var result = await ShowDialogSafelyAsync(dialog);

        if (result == ContentDialogResult.Primary)
        {
            int targetColorCount = (int)numberBox.Value;
            if (targetColorCount > 0)
            {
                DrillGridManager.SelectedGrid.ReduceColors(targetColorCount);
                DrillGridManager.MarkDirty(true);
                UpdateWindowTitle();
                UpdateMinCountUI();
            }
        }
    }

    private void UpdateMinCountUI()
    {
        if (MinCountSlider == null || MinCountValueText == null || MinCountSubtext == null || ApplyMinCountButton == null)
            return;

        int minCount = (int)Math.Round(MinCountSlider.Value);
        MinCountValueText.Text = $"{minCount} drills";

        if (DrillGridManager?.SelectedGrid != null)
        {
            int belowThreshold = DrillGridManager.GetColorsBelowThresholdCount(minCount);
            MinCountSubtext.Text = belowThreshold == 1
                ? "1 color below threshold"
                : $"{belowThreshold} colors below threshold";
            ApplyMinCountButton.IsEnabled = belowThreshold > 0;
        }
        else
        {
            MinCountSubtext.Text = "No active grid";
            ApplyMinCountButton.IsEnabled = false;
        }
    }

    private void MinCountSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateMinCountUI();
    }

    private async void ApplyMinCountButton_Click(object sender, RoutedEventArgs e)
    {
        if (DrillGridManager?.SelectedGrid == null) return;
        int minCount = (int)Math.Round(MinCountSlider.Value);
        if (minCount <= 1) return;

        int eliminated = await DrillGridManager.EnforceMinimumColorCountAsync(minCount);
        UpdateMinCountUI();
        UpdateVersionsList();
        UpdateWindowTitle();
        if (eliminated > 0)
        {
            _logger.LogInformation("Enforced min drill count {MinCount}: eliminated {Count} colors", minCount, eliminated);
        }
    }

    private async void ColorThresholdButton_Click(object sender, RoutedEventArgs e)
    {
        if (DrillGridManager?.SelectedGrid == null) return;

        int initialValue = (int)Math.Round(MinCountSlider.Value);
        if (initialValue < 1) initialValue = 5;

        var slider = new Slider
        {
            Minimum = 1,
            Maximum = 50,
            StepFrequency = 1,
            Value = initialValue
        };

        var previewText = new TextBlock
        {
            FontSize = 13,
            Margin = new Thickness(0, 8, 0, 0),
            Opacity = 0.8
        };

        void UpdatePreview(int val)
        {
            int count = DrillGridManager.GetColorsBelowThresholdCount(val);
            previewText.Text = $"Threshold: {val} drills ({count} color{(count == 1 ? "" : "s")} will be merged into closest palette colors)";
        }

        UpdatePreview(initialValue);
        slider.ValueChanged += (s, args) =>
        {
            UpdatePreview((int)Math.Round(args.NewValue));
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Set the minimum number of drill instances required per color. Any colors with fewer drills will be merged into the closest matching color in your palette to eliminate confetti.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Opacity = 0.8
                },
                slider,
                previewText
            }
        };

        var dialog = new ContentDialog
        {
            Title = "Minimum Color Count Threshold",
            Content = panel,
            PrimaryButtonText = "Apply Threshold",
            CloseButtonText = "Cancel",
            XamlRoot = this.Content.XamlRoot
        };

        var result = await ShowDialogSafelyAsync(dialog);
        if (result == ContentDialogResult.Primary)
        {
            int targetThreshold = (int)Math.Round(slider.Value);
            MinCountSlider.Value = targetThreshold;
            if (targetThreshold > 1)
            {
                int eliminated = await DrillGridManager.EnforceMinimumColorCountAsync(targetThreshold);
                UpdateMinCountUI();
                UpdateVersionsList();
                UpdateWindowTitle();
                _logger.LogInformation("Color threshold applied via dialog: {Threshold}, eliminated {Eliminated} colors", targetThreshold, eliminated);
            }
        }
    }

    private void ClearHighlight_Click(object sender, RoutedEventArgs e)
    {
        _sharedInteractionService.HighlightedColor = null;
        // Assuming the color summary legend is what's driving the selection
        MyDrillGridView.ClearColorSummaryLegendSelection();
    }

    // INotifyPropertyChanged helper
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}