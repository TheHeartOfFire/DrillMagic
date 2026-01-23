using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using DrillMagic.Windows.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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

    // Services
    private readonly IColorMapService _colorMapService;
    private readonly IPdfGenerator _pdfGenerator;

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
        _colorMapService = App.Current.Services!.GetRequiredService<IColorMapService>();
        _pdfGenerator = App.Current.Services!.GetRequiredService<IPdfGenerator>();

        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.Loaded += MainWindow_Loaded;
        }
    }

    // Lifecycle / initialization
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await PopulateToolkitPaletteAsync();
        DrillGridManager = App.Current.Services!.GetRequiredService<IDrillGridManager>();
        DrillGridManager.PropertyChanged += DrillGridManager_PropertyChanged;
        _sharedInteractionService = App.Current.Services!.GetRequiredService<ISharedInteractionService>();
        _sharedInteractionService.PropertyChanged += InteractionServicePropertyChanged;
        
        // Data initialization happens in the service constructor now, 
        // so we just update the UI.
        UpdateFilteredColors();
    }

    // DrillGridManager property change handler
    private void DrillGridManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IDrillGridManager.SelectedGrid))
        {
            MyDrillGridView.Grid = DrillGridManager?.SelectedGrid;
        }
    }

    // Interaction service property change handler
    private void InteractionServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SharedInteractionService.CurrentMode))
        {
            UpdateInteractionButtons();
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

    // Formatting helper
    public string FormatCellSize(double value) => $"Cell Size: {(int)value}";

    // Toolkit UI handlers
    private async void ToolkitColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs e)
    {
        if (_suppressColorChanged) return;

        var picked = e.NewColor;

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
        var fileOpenPicker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        fileOpenPicker.FileTypeFilter.Add(".jpg");
        fileOpenPicker.FileTypeFilter.Add(".jpeg");
        fileOpenPicker.FileTypeFilter.Add(".png");
        fileOpenPicker.FileTypeFilter.Add(".bmp");

        InitializeWithWindow.Initialize(fileOpenPicker, WindowNative.GetWindowHandle(this));

        var file = await fileOpenPicker.PickSingleFileAsync();
        if (file != null)
        {
            var imageStream = await GetStreamFromFileAsync(file);
            if (imageStream is null) return;

            await DrillGridManager.LoadImageAsync(imageStream);
            if (DrillGridManager.AvailableCellSizes.Any())
            {
                CellSizeSlider.Minimum = DrillGridManager.AvailableCellSizes.Min();
                CellSizeSlider.Maximum = DrillGridManager.AvailableCellSizes.Max();
                CellSizeSlider.Value = DrillGridManager.AvailableCellSizes[9];
                CellSizePanel.Visibility = Visibility.Visible;
                CellSizeSeparator.Visibility = Visibility.Visible;
            }
            else
            {
                CellSizePanel.Visibility = Visibility.Collapsed;
                CellSizeSeparator.Visibility = Visibility.Collapsed;
            }
        }
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
            await ShowErrorDialog("Error Reading File", $"Could not read the selected file. Please ensure it is accessible and not corrupted.\n\nDetails: {ex.Message}");
            return null;
        }
    }

    private async void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        if (MyDrillGridView.Grid is null) return;

        var savePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "DrillMagicGrid"
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
                await ShowErrorDialog("PDF Generation Failed", errorContent);
                return;
            }

            await File.WriteAllBytesAsync(file.Path, pdfBytes);
            
            // Optional: You could show a "Success" dialog or open the file automatically.
            // await ShowErrorDialog("Success", "PDF saved successfully.");
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("Failed to Save PDF", $"An error occurred while saving the file: {ex.Message}");
        }
    }

    private async Task ShowErrorDialog(string title, string content)
    {
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
        await dialog.ShowAsync();
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
        if (DrillGridManager is not null)
        {
            uint newSize = (uint)e.NewValue;
            await DrillGridManager.SelectGridAsync(newSize);
        }
    }

    private void SymbolOverlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (MyDrillGridView != null)
        {
            MyDrillGridView.IsSymbolOverlayEnabled = SymbolOverlayButton.IsChecked ?? false;
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

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            int targetColorCount = (int)numberBox.Value;
            if (targetColorCount > 0)
            {
                DrillGridManager.SelectedGrid.ReduceColors(targetColorCount);
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