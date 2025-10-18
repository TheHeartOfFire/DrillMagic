using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

    public IDrillGridManager DrillGridManager { get; }

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

    public MainWindow()
    {
        InitializeComponent();
        DrillGridManager = App.Current.Services.GetRequiredService<IDrillGridManager>();
        if (ColorMap.DefaultColorMap.Count == 0)
        {
            ColorMap.Initialize();
        }
        UpdateFilteredColors();
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

    private void PrintButton_Click(object sender, RoutedEventArgs e) { }
    private void PointerButton_Click(object sender, RoutedEventArgs e) { }
    private void PaintBucketButton_Click(object sender, RoutedEventArgs e) { }
    private void ColorInspectorButton_Click(object sender, RoutedEventArgs e) { }
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
}