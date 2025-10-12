using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using DrillMagic.Windows.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;

namespace DrillMagic.Windows;
public sealed partial class MainWindow : WindowEx, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<DMCColor> FilteredColors { get; set; } = [];
    
    private DrillGrid? _drillGrid;
    public DrillGrid? DrillGrid
    {
        get => _drillGrid;
        set
        {
            _drillGrid = value;
            OnPropertyChanged();
        }
    }

    public string SelectedColorName { get; set; } = "None";

    public MainWindow()
    {
        InitializeComponent();
        App.InteractionService.PropertyChanged += InteractionServicePropertyChanged;
        UpdateInteractionButtons();
        FilteredColors = new(ColorMap.DefaultColorMap.Values);
    }
    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null) return;

        var fileOpenPicker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(fileOpenPicker, hwnd);

        fileOpenPicker.FileTypeFilter.Add(".png");
        fileOpenPicker.FileTypeFilter.Add(".jpg");
        fileOpenPicker.FileTypeFilter.Add(".bmp");

        var file = await fileOpenPicker.PickSingleFileAsync();
        if (file != null)
        {
            DrillGrid = new DrillGrid(file.Path, 10);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Exit();
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
            var (pdfBytes, errorMessage) = await PdfGenerator.GenerateDrillGridPdf(MyDrillGridView.Grid);

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
        else if(e.PropertyName == nameof(SharedInteractionService.InspectedColor) &&
           sender is SharedInteractionService service)
        {
            SelectedColorName = App.InteractionService.InspectedColor.Name;
            ColorGridView.SelectedItem = App.InteractionService.InspectedColor.Color;
            ColorSearchBox.Text = SelectedColorName;
        }
    }

    private void UpdateInteractionButtons()
    {
        PointerButton.IsChecked = App.InteractionService.CurrentMode == InteractionMode.Navigate;
        PaintBucketButton.IsChecked = App.InteractionService.CurrentMode == InteractionMode.Coloring;
        ColorInspectorButton.IsChecked = App.InteractionService.CurrentMode == InteractionMode.ColorInspector;
    }

    private void PointerButton_Click(object sender, RoutedEventArgs e)
    {
        App.InteractionService.CurrentMode = InteractionMode.Navigate;
    }

    private void PaintBucketButton_Click(object sender, RoutedEventArgs e)
    {
        App.InteractionService.CurrentMode = InteractionMode.Coloring;
    }

    private void ColorInspectorButton_Click(object sender, RoutedEventArgs e)
    {
        App.InteractionService.CurrentMode = InteractionMode.ColorInspector;
    }

    private void ColorSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = ColorSearchBox.Text?.Trim().ToLower() ?? "";
        FilteredColors.Clear();
        foreach (var color in ColorMap.DefaultColorMap.Values.Where(c => c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            FilteredColors.Add(color);
    }

    private void ColorGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ColorGridView.SelectedItem is DMCColor selected)
        {
            SelectedColorName = selected.Name;
            App.InteractionService.SelectedColor = selected.Color;
        }
        else
        {
            SelectedColorName = "None";
        }
        OnPropertyChanged(nameof(SelectedColorName));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}