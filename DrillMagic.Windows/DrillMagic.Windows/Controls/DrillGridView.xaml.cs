using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Models;
using DrillMagic.Windows.Services;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace DrillMagic.Windows.Controls;
public sealed partial class DrillGridView : UserControl
{
    public static readonly DependencyProperty GridProperty =
        DependencyProperty.Register(nameof(Grid), typeof(DrillGrid), typeof(DrillGridView), new PropertyMetadata(null, OnGridChanged));

    public DrillGrid? Grid
    {
        get => (DrillGrid?)GetValue(GridProperty);
        set => SetValue(GridProperty, value);
    }

    private Matrix3x2 _transform = Matrix3x2.Identity;
    private Point? _lastPointerPosition;
    private bool _isCentered = false;
    private const float MinZoom = 3f;
    private const float MaxZoom = 30f;
    private const float SymbolVisibilityZoomThreshold = 7.5f;

    private readonly Dictionary<System.Drawing.Color, string> _colorToSymbolMap = [];

    public DrillGridView()
    {
        InitializeComponent();
        // Pointer events
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCanceled += OnPointerReleased;

        // Mouse wheel for zooming
        PointerWheelChanged += OnPointerWheelChanged;
    }

    private static void OnGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DrillGridView)d;
        if (e.OldValue is DrillGrid oldGrid)
        {
            oldGrid.PropertyChanged -= view.OnGridPropertyChanged;
        }
        if (e.NewValue is DrillGrid newGrid)
        {
            view._transform = Matrix3x2.Identity;
            view._isCentered = false;
            view.UpdateColorToSymbolMap();
            newGrid.PropertyChanged += view.OnGridPropertyChanged;
        }
        view.Canvas.Invalidate();
    }

    private void OnGridPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DrillGrid.ColorSummary))
        {
            ColorSummaryLegend.Summary = Grid?.ColorSummary;
            UpdateColorToSymbolMap();
            Canvas.Invalidate();
        }
    }

    private void UpdateColorToSymbolMap()
    {
        _colorToSymbolMap.Clear();
        if (Grid?.ColorSummary is null) return;

        int currentSymbolIndex = 0;
        foreach (var color in Grid.ColorSummary.OrderByDescending(kvp => kvp.Value))
        {
            _colorToSymbolMap[color.Key] = ColorSummaryLegend.Symbols[currentSymbolIndex].ToString();
            currentSymbolIndex++;
        }
    }

    private void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args) { }

    private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (Grid is null) return;

        if (!_isCentered)
        {
            var canvasWidth = (float)sender.ActualWidth;
            var canvasHeight = (float)sender.ActualHeight;
            var gridWidth = (float)Grid.Width;
            var gridHeight = (float)Grid.Height;

            if (gridWidth > 0 && gridHeight > 0)
            {
                var scaleX = canvasWidth / gridWidth;
                var scaleY = canvasHeight / gridHeight;
                var scale = Math.Min(scaleX, scaleY);

                var scaledGridWidth = gridWidth * scale;
                var scaledGridHeight = gridHeight * scale;
                var tx = (canvasWidth - scaledGridWidth) / 2f;
                var ty = (canvasHeight - scaledGridHeight) / 2f;

                _transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(tx, ty);
            }
            _isCentered = true;
        }

        args.DrawingSession.Transform = _transform;
        var ds = args.DrawingSession;

        Matrix3x2.Invert(_transform, out var inverseTransform);
        var topLeft = Vector2.Transform(Vector2.Zero, inverseTransform);
        var bottomRight = Vector2.Transform(new Vector2((float)sender.ActualWidth, (float)sender.ActualHeight), inverseTransform);

        int startX = (int)Math.Max(0, Math.Floor(topLeft.X));
        int startY = (int)Math.Max(0, Math.Floor(topLeft.Y));
        int endX = (int)Math.Min(Grid.Width, Math.Ceiling(bottomRight.X));
        int endY = (int)Math.Min(Grid.Height, Math.Ceiling(bottomRight.Y));

        bool shouldDrawSymbols = _transform.M11 >= SymbolVisibilityZoomThreshold;
        using var textFormat = shouldDrawSymbols ? new CanvasTextFormat
        {
            FontFamily = "Segoe UI",
            FontSize = 0.7f,
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center,
            WordWrapping = CanvasWordWrapping.NoWrap
        } : null;

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                // Use System.Drawing.Color directly from the grid
                var cellColor = Grid.Grid[y, x];
                if (cellColor.A > 0)
                {
                    // Convert System.Drawing.Color to Windows.UI.Color for drawing
                    var uiColor = Color.FromArgb(cellColor.A, cellColor.R, cellColor.G, cellColor.B);
                    ds.FillRectangle(x, y, 1, 1, uiColor);

                    if (shouldDrawSymbols && textFormat is not null && _colorToSymbolMap.TryGetValue(cellColor, out var symbol))
                    {
                        var brightness = (0.299 * cellColor.R + 0.587 * cellColor.G + 0.114 * cellColor.B) / 255;
                        var symbolColor = brightness > 0.5 ? Colors.Black : Colors.White;
                        ds.DrawText(symbol, x + 0.5f, y + 0.5f, symbolColor, textFormat);
                    }
                }
            }
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this);
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse || !properties.Properties.IsLeftButtonPressed)
        {
            return;
        }

        switch (App.InteractionService.CurrentMode)
        {
            case InteractionMode.Navigate:
                _lastPointerPosition = properties.Position;
                CapturePointer(e.Pointer);
                break;
            case InteractionMode.Coloring:
                ApplyPaintBucket(properties.Position);
                break;
            case InteractionMode.ColorInspector:
                CaptureColor(properties.Position);
                break;
        }
    }

    private (int x, int y)? GetGridPosition(Point position)
    {
        if (Grid is null) return null;

        Matrix3x2.Invert(_transform, out var inverseTransform);
        var gridPosition = Vector2.Transform(position.ToVector2(), inverseTransform);

        int x = (int)Math.Floor(gridPosition.X);
        int y = (int)Math.Floor(gridPosition.Y);

        return (x >= 0 && x < Grid.Width && y >= 0 && y < Grid.Height) ? (x, y) : null;
    }

    private void CaptureColor(Point position)
    {
        var gridPosition = GetGridPosition(position);

        if (!gridPosition.HasValue) return;

        App.InteractionService.InspectedColor = ColorMap.GetDMCColor(Grid!.Grid[gridPosition.Value.y, gridPosition.Value.x]);
    }

    private void ApplyPaintBucket(Point position)
    {
        var gridPosition = GetGridPosition(position);

        if (!gridPosition.HasValue) return;

        Grid!.Grid[gridPosition.Value.y, gridPosition.Value.x] = App.InteractionService.SelectedColor;
        Grid.RecalculateColorSummary();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_lastPointerPosition.HasValue && Grid is not null && App.InteractionService.CurrentMode == InteractionMode.Navigate)
        {
            var currentPosition = e.GetCurrentPoint(this).Position;
            var delta = new Vector2(
                (float)(currentPosition.X - _lastPointerPosition.Value.X),
                (float)(currentPosition.Y - _lastPointerPosition.Value.Y));

            var proposedTranslation = _transform.Translation + delta;
            _transform.Translation = ClampTranslation(proposedTranslation);

            _lastPointerPosition = currentPosition;
            Canvas.Invalidate();
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _lastPointerPosition = null;
        ReleasePointerCapture(e.Pointer);
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (Grid is null) return;

        var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        var scaleFactor = delta > 0 ? 1.1f : 1 / 1.1f;

        var currentZoom = _transform.M11;
        var newZoom = Math.Clamp(currentZoom * scaleFactor, MinZoom, MaxZoom);
        scaleFactor = newZoom / currentZoom;

        if (Math.Abs(scaleFactor - 1.0f) < 0.001f) return;

        var gridCenterWorld = new Vector2(Grid.Width / 2.0f, Grid.Height / 2.0f);

        var centerPosBefore = Vector2.Transform(gridCenterWorld, _transform);
        _transform.M11 = newZoom;
        _transform.M22 = newZoom;
        var centerPosAfter = Vector2.Transform(gridCenterWorld, _transform);

        var error = centerPosBefore - centerPosAfter;
        _transform.Translation += error;
        _transform.Translation = ClampTranslation(_transform.Translation);

        Canvas.Invalidate();
    }

    private Vector2 ClampTranslation(Vector2 proposedTranslation)
    {
        if (Grid is null) return proposedTranslation;

        var canvasWidth = (float)Canvas.ActualWidth;
        var canvasHeight = (float)Canvas.ActualHeight;
        var scale = _transform.M11;

        var gridScreenWidth = Grid.Width * scale;
        var gridScreenHeight = Grid.Height * scale;

        float minX, maxX, minY, maxY;

        if (gridScreenWidth < canvasWidth)
        {
            minX = 0;
            maxX = canvasWidth - gridScreenWidth;
        }
        else
        {
            minX = canvasWidth - gridScreenWidth;
            maxX = 0;
        }

        if (gridScreenHeight < canvasHeight)
        {
            minY = 0;
            maxY = canvasHeight - gridScreenHeight;
        }
        else
        {
            minY = canvasHeight - gridScreenHeight;
            maxY = 0;
        }

        return new Vector2(
            Math.Clamp(proposedTranslation.X, minX, maxX),
            Math.Clamp(proposedTranslation.Y, minY, maxY)
        );
    }
}