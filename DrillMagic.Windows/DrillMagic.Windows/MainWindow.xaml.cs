using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.Storage.Pickers;
using WinUIEx;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace DrillMagic.Windows;

public sealed partial class MainWindow : WinUIEx.WindowEx, INotifyPropertyChanged
{
    public ObservableCollection<DMCColor> FilteredColors { get; } = new();
    private List<DMCColor> _allColors;
    private string _selectedColorName;
    public string SelectedColorName
    {
        get => _selectedColorName;
        set { _selectedColorName = value; OnPropertyChanged(nameof(SelectedColorName)); }
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        this.InitializeComponent();
        this.TaskBarIcon = Icon.FromFile("Resources/Logo.ico");
        if (ColorMap.DefaultColorMap.Count == 0)
            ColorMap.Initialize();

        ColorGridView.ItemsSource = ColorMap.DefaultColorMap.Values.ToList();
        if (ColorGridView.Items.Count > 0)
        {
            ColorGridView.SelectedIndex = 0;
        }
        PointerButton.IsChecked = true;

        LoadAndSortColors();
        SelectedColorName = "";

        App.InteractionService.PropertyChanged += InteractionServicePropertyChanged;
    }

    private void InteractionServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName == nameof(SharedInteractionService.InspectedColor) &&
           sender is SharedInteractionService service)
        {
            SelectedColorName = App.InteractionService.InspectedColor.Name;
            ColorGridView.SelectedItem = App.InteractionService.InspectedColor.Color;
            ColorSearchBox.Text = SelectedColorName;
        }
    }

    private void LoadAndSortColors()
    {
        // Assign a symbol to each color (A, B, C, ...)
        _allColors = DrillMagic.Core.Types.ColorMap.DefaultColorMap.Values
            .OrderBy(c => GetColorSortKey(c.Color))
            .ToList();

        FilteredColors.Clear();
        foreach (var color in _allColors)
            FilteredColors.Add(color);
    }

    private static (float Hue, float Sat, float Bright) GetColorSortKey(System.Drawing.Color color)
    {
        // Convert RGB to HSB for sorting
        float hue = color.GetHue();
        float sat = color.GetSaturation();
        float bright = color.GetBrightness();
        return (hue, sat, bright);
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

    private void PointerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true } clickedButton)
        {
            App.InteractionService.CurrentMode = InteractionMode.Navigate;
            if (clickedButton == PointerButton)
            {
                PaintBucketButton.IsChecked = false;
                ColorInspectorButton.IsChecked = false;
            }
        }
        else
        {
            // Prevent unchecking
            (sender as ToggleButton)!.IsChecked = true;
        }
    }

    private void PaintBucketButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true } clickedButton)
        {
            App.InteractionService.CurrentMode = InteractionMode.Coloring;
            if (clickedButton == PaintBucketButton)
            {
                PointerButton.IsChecked = false;
                ColorInspectorButton.IsChecked = false;
            }
        }
        else
        {
            // Prevent unchecking
            (sender as ToggleButton)!.IsChecked = true;
        }
    }

    private void ColorInspectorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { IsChecked: true } clickedButton)
        {
            App.InteractionService.CurrentMode = InteractionMode.ColorInspector;
            if (clickedButton == ColorInspectorButton)
            {
                PointerButton.IsChecked = false;
                PaintBucketButton.IsChecked = false;
            }
        }
        else
        {
            // Prevent unchecking
            (sender as ToggleButton)!.IsChecked = true;
        }
    }

    private void ColorSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = ColorSearchBox.Text?.Trim().ToLower() ?? "";
        FilteredColors.Clear();
        foreach (var color in _allColors.Where(c => c.Name.ToLower().Contains(query)))
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
            SelectedColorName = "";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}