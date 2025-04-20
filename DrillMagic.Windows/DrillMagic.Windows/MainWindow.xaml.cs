using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using CommunityToolkit.WinUI.Controls.RibbonRns;
using CommunityToolkit.WinUI.Controls;
using System.Collections.ObjectModel;
using Microsoft.UI;
using Windows.System;
using Windows.UI.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DrillMagic.Windows;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : WinUIEx.WindowEx
{
    public uint CellSize = 15;
    public ObservableCollection<int> Grid;
    private bool _enterIsPressed = false;
    private bool _ctrlIsPressed = false;
    public MainWindow()
    {
        this.InitializeComponent();
        Grid = [];

    }

    private void CoreWindow_PointerWheelChanged(CoreWindow sender, PointerEventArgs e)
    {
        // Get the scroll amount from the mouse wheel
        var scrollAmount = e.CurrentPoint.Properties.MouseWheelDelta;

        if (_ctrlIsPressed)
        {
            if (_enterIsPressed)
            {
                // Horizontal scroll (use scrollAmount for horizontal movement)
                DisplayCanvas.Translation += new System.Numerics.Vector3((float)(scrollAmount), 0, 0);
                return;
            }
            DisplayCanvas.Translation += new System.Numerics.Vector3(0, (float)(scrollAmount), 0);
            // Vertical scroll (use scrollAmount for vertical movement)
            return;
        }
        // Zoom (use scrollAmount to adjust zoom level)
        DisplayCanvas.Scale += new System.Numerics.Vector3((float)(.001 * scrollAmount), (float)(.001 * scrollAmount), 0);
    }

    private void CoreWindow_KeyUp(CoreWindow sender, KeyEventArgs e)
    {

        if (e.VirtualKey == VirtualKey.Enter)
            _enterIsPressed = false;
        if (e.VirtualKey == VirtualKey.Control)
            _ctrlIsPressed = false;
    }

    private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs e)
    {
        if (e.VirtualKey == VirtualKey.Enter)
            _enterIsPressed = true;
        if (e.VirtualKey == VirtualKey.Control)
            _ctrlIsPressed = true;
    }

    private void BackgroundImage_ImageOpened(object sender, RoutedEventArgs e)
    {
        double w = BackgroundImage.ActualWidth / CellSize;
        double h = BackgroundImage.ActualHeight / CellSize;

        for (int i = 0; i < w * h; i++)
        {
            Grid.Add(i);
        }
        
        CellRepeater.ItemsSource = Grid;
        CellRepeater.MaxWidth = BackgroundImage.ActualWidth;
        CellRepeater.Width = BackgroundImage.ActualWidth;
        CellRepeater.MinHeight = BackgroundImage.ActualHeight;
        CellRepeater.Height = BackgroundImage.ActualHeight;
    }

    private void Canvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // Get the scroll amount from the mouse wheel
        var scrollAmount = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;

        if (_enterIsPressed)
        {
            if (_ctrlIsPressed)
            {
                // Horizontal scroll (use scrollAmount for horizontal movement)
                DisplayCanvas.Translation += new System.Numerics.Vector3( (float)(scrollAmount), 0, 0);
                return;
            }
            DisplayCanvas.Translation += new System.Numerics.Vector3(0, (float)(scrollAmount), 0);
            // Vertical scroll (use scrollAmount for vertical movement)
            return;
        }
        // Zoom (use scrollAmount to adjust zoom level)
        DisplayCanvas.Scale += new System.Numerics.Vector3((float)(.001*scrollAmount), (float)(.001 * scrollAmount), 0);
    }


    private void Canvas_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
            _enterIsPressed = true;
        if (e.Key == VirtualKey.Control)
            _ctrlIsPressed = true;
    }

    private void Canvas_KeyUp(object sender, KeyRoutedEventArgs e)
    {

        if (e.Key == VirtualKey.Enter)
            _enterIsPressed = false;
        if (e.Key == VirtualKey.Control)
            _ctrlIsPressed = false;
    }
}
