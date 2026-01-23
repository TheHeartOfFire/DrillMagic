using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace DrillMagic.Windows.Controls;

public sealed partial class DrillGridView : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty GridProperty =
        DependencyProperty.Register(nameof(Grid), typeof(DrillGrid), typeof(DrillGridView), new PropertyMetadata(null, OnGridChanged));

    public DrillGrid Grid
    {
        get => (DrillGrid)GetValue(GridProperty);
        set => SetValue(GridProperty, value);
    }

    public static readonly DependencyProperty IsSymbolOverlayEnabledProperty =
        DependencyProperty.Register(nameof(IsSymbolOverlayEnabled), typeof(bool), typeof(DrillGridView), new PropertyMetadata(true, OnSymbolOverlayChanged));

    public bool IsSymbolOverlayEnabled
    {
        get => (bool)GetValue(IsSymbolOverlayEnabledProperty);
        set => SetValue(IsSymbolOverlayEnabledProperty, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly ISharedInteractionService _sharedInteractionService;
    // Explicitly using the interface to avoid any confusion with the ColorMap class
    private readonly IColorMapService _colorMapService; 
    private DMCColor? _highlightedColor;

    private Matrix3x2 _transform = Matrix3x2.Identity;
    private Point? _lastPointerPosition;
    private bool _isCentered = false;
    private const float MinZoom = 3f;
    private const float MaxZoom = 30f;
    private const float SymbolVisibilityZoomThreshold = 10f;

    private readonly Dictionary<System.Drawing.Color, string> _colorToSymbolMap = [];
    
    public DrillGridView()
    {
        InitializeComponent();
        _sharedInteractionService = App.Current.Services.GetRequiredService<ISharedInteractionService>();
        _colorMapService = App.Current.Services.GetRequiredService<IColorMapService>();
        
        _sharedInteractionService.PropertyChanged += OnInteractionServicePropertyChanged;
        // Pointer events
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCanceled += OnPointerReleased;

        // Mouse wheel for zooming
        PointerWheelChanged += OnPointerWheelChanged;
    }

    private void OnInteractionServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SharedInteractionService.HighlightedColor))
        {
            _highlightedColor = _sharedInteractionService.HighlightedColor;
            Canvas.Invalidate(); // Redraw the canvas to apply/remove highlight
        }
    }
    public void ClearColorSummaryLegendSelection()
    {
        if (ColorSummaryLegend.FindName("ColorSummaryGrid") is GridView gridView)
        {
            gridView.SelectedItem = null;
        }
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

    private void OnGridPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DrillGrid.ColorSummary))
        {
            ColorSummaryLegend.Summary = Grid?.ColorSummary ?? [];
            UpdateColorToSymbolMap();
        }
        // Any property change on the grid should trigger a redraw
        Canvas.Invalidate();
    }

    private void UpdateColorToSymbolMap()
    {
        _colorToSymbolMap.Clear();
        if (Grid?.ColorSummary is null) return;

        int currentSymbolIndex = 0;
        foreach (var color in Grid.ColorSummary.OrderByDescending(kvp => kvp.Value))
        {
            if (currentSymbolIndex < ColorSummaryLegend.Symbols.Length)
            {
                _colorToSymbolMap[color.Key] = ColorSummaryLegend.Symbols[currentSymbolIndex].ToString();
                currentSymbolIndex++;
            }
        }
    }

    private static void OnSymbolOverlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DrillGridView)d;
        control.Canvas.Invalidate();
    }

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

        bool shouldDrawSymbols = IsSymbolOverlayEnabled && _transform.M11 >= SymbolVisibilityZoomThreshold;
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
                var cellColor = Grid.Grid[y, x];
                if (cellColor.A == 0) continue;

                var uiColor = Color.FromArgb(cellColor.A, cellColor.R, cellColor.G, cellColor.B);
                ds.FillRectangle(x, y, 1, 1, uiColor);

                if (_highlightedColor is not null)
                {
                    if (uiColor == Color.FromArgb(
                        _highlightedColor.Color.A, 
                        _highlightedColor.Color.R, 
                        _highlightedColor.Color.G,
                        _highlightedColor.Color.B))
                    {
                        // Use a semi-transparent yellow border for highlighting
                        ds.DrawRectangle(x, y, 1, 1, Color.FromArgb(180, 255, 255, 0), 0.1f);
                    }
                    else
                    {
                        // De-emphasize non-matching cells with a dark overlay
                        ds.FillRectangle(x, y, 1, 1, Color.FromArgb(128, 0, 0, 0));
                    }
                }
                if (shouldDrawSymbols && textFormat is not null && _colorToSymbolMap.TryGetValue(cellColor, out var symbol))
                {
                    var brightness = (0.299 * cellColor.R + 0.587 * cellColor.G + 0.114 * cellColor.B) / 255;
                    var symbolColor = brightness > 0.5 ? Colors.Black : Colors.White;
                    ds.DrawText(symbol, x + 0.5f, y + 0.5f, symbolColor, textFormat);
                }
            }
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this);
        var isLeftButtonPressed = properties.Properties.IsLeftButtonPressed;
        var isMiddleButtonPressed = properties.Properties.IsMiddleButtonPressed;

        if (_sharedInteractionService.CurrentMode is InteractionMode.Navigate &&
            e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && (isLeftButtonPressed || isMiddleButtonPressed))
        {
            _lastPointerPosition = properties.Position;
            CapturePointer(e.Pointer);
        }

        if (!isLeftButtonPressed) return;

        switch (_sharedInteractionService.CurrentMode)
        {
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

        // Ensuring we use the service instance to look up the color
        _sharedInteractionService.InspectedColor = _colorMapService.GetDMCColor(Grid!.Grid[gridPosition.Value.y, gridPosition.Value.x]);
    }

    private void ApplyPaintBucket(Point position)
    {
        var gridPosition = GetGridPosition(position);

        if (!gridPosition.HasValue) return;

        Grid!.Grid[gridPosition.Value.y, gridPosition.Value.x] = _sharedInteractionService.SelectedColor;
        Grid.RecalculateColorSummary();
    }



    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_lastPointerPosition.HasValue && Grid is not null)
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

        var pointerPosition = e.GetCurrentPoint(Canvas).Position.ToVector2();
        var worldPosition = Vector2.Transform(pointerPosition, Matrix3x2.Invert(_transform, out var inv) ? inv : Matrix3x2.Identity);

        _transform = _transform * Matrix3x2.CreateScale(scaleFactor, pointerPosition);
        _transform.M11 = Math.Clamp(_transform.M11, MinZoom, MaxZoom);
        _transform.M22 = Math.Clamp(_transform.M22, MinZoom, MaxZoom);
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