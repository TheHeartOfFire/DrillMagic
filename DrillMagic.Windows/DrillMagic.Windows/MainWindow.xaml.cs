using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WinRT.Interop;
using Windows.Storage.Pickers;
using WinUIEx;
using DrillMagic.Windows.Utils;

namespace DrillMagic.Windows;
public sealed partial class MainWindow : WindowEx, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public IDrillGridManager? DrillGridManager { get; private set; }

    public ObservableCollection<DMCColor> FilteredColors { get; set; } = [];
    private string _selectedColorName = "None";
    public string SelectedColorName
    {
        get => _selectedColorName;
        set
        {
            _selectedColorName = value;
            OnPropertyChanged();
        }
    }
    private SharedInteractionService? _sharedInteractionService;

    public MainWindow()
    {
        InitializeComponent(); 
        
        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.Loaded += MainWindow_Loaded;
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        DrillGridManager = App.Current.Services!.GetRequiredService<IDrillGridManager>();
        _sharedInteractionService = App.Current.Services!.GetRequiredService<SharedInteractionService>();
        _sharedInteractionService.PropertyChanged += InteractionServicePropertyChanged;
        await InitializeDataAsync();
    }


    private async Task InitializeDataAsync()
    {
        if (ColorMap.DefaultColorMap.Count == 0)
        {
            await Task.Run(() => ColorMap.Initialize());
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateFilteredColors();
        });
    }
    public string FormatCellSize(double value) => $"Cell Size: {(int)value}";

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
            await DrillGridManager.LoadImageAsync(file.Path);
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
            // Call the new PDFsharp-based generator.
            var (pdfBytes, errorMessage) = await PdfSharpGenerator.GenerateDrillGridPdf(MyDrillGridView.Grid);

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
    private void InteractionServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SharedInteractionService.CurrentMode))
        {
            UpdateInteractionButtons();
        }
        else if (e.PropertyName == nameof(SharedInteractionService.InspectedColor) &&
           sender is SharedInteractionService service)
        {
            var dmcColor = service.InspectedColor;
            SelectedColorName = dmcColor.Name;
            ColorGridView.SelectedItem = dmcColor;
            ColorSearchBox.Text = SelectedColorName;
        }
        else if (e.PropertyName == nameof(SharedInteractionService.HighlightedColor))
        {
            ClearHighlightButton.Visibility = _sharedInteractionService.HighlightedColor is not null
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (_sharedInteractionService.HighlightedColor is not null)
            {
                var dmcColor = _sharedInteractionService.HighlightedColor;
                SelectedColorName = dmcColor.Name;
                ColorGridView.SelectedItem = dmcColor;
                ColorSearchBox.Text = SelectedColorName;
            }
        }
    }

    private void UpdateInteractionButtons()
    {
        PointerButton.IsChecked = _sharedInteractionService.CurrentMode == InteractionMode.Navigate;
        PaintBucketButton.IsChecked = _sharedInteractionService.CurrentMode == InteractionMode.Coloring;
        ColorInspectorButton.IsChecked = _sharedInteractionService.CurrentMode == InteractionMode.ColorInspector;
    }

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
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void ColorSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateFilteredColors();
    }

    private void UpdateFilteredColors()
    {
        var searchText = ColorSearchBox.Text.ToLower();
        var allColors = ColorMap.DefaultColorMap.Values;
        var filtered = string.IsNullOrWhiteSpace(searchText)
            ? allColors
            : allColors.Where(c => c.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) || c.DMCNumber.ToString().Contains(searchText));

        FilteredColors.Clear();
        foreach (var color in filtered)
        {
            FilteredColors.Add(color);
        }
    }

    private void ColorGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is DMCColor selected)
        {
            _sharedInteractionService.SelectedColor = selected.Color;
            SelectedColorName = selected.Name;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void CellSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
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
}