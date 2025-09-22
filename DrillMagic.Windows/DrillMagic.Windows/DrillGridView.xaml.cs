using DrillMagic.Core;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace DrillMagic.Windows;
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
    private const float MinZoom = 0.1f;
    private const float MaxZoom = 30f;

    public DrillGridView()
    {
        InitializeComponent();
        // Pointer events for panning
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
        if (view.Grid != null)
        {
            // Reset transform when a new grid is loaded.
            view._transform = Matrix3x2.Identity;
        }
        view.Canvas.Invalidate(); // Redraw when the grid data changes
    }

    private void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        // You can create long-lived resources here, like text formats or effects.
    }

    private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (Grid is null) return;

        args.DrawingSession.Transform = _transform;
        var ds = args.DrawingSession;

        // For performance, calculate the visible range of cells
        var inverseTransform = Matrix3x2.Invert(_transform, out var inv) ? inv : Matrix3x2.Identity;
        var topLeft = Vector2.Transform(Vector2.Zero, inverseTransform);
        var bottomRight = Vector2.Transform(new Vector2((float)sender.ActualWidth, (float)sender.ActualHeight), inverseTransform);

        int startX = (int)Math.Max(0, Math.Floor(topLeft.X));
        int startY = (int)Math.Max(0, Math.Floor(topLeft.Y));
        int endX = (int)Math.Min(Grid.Width, Math.Ceiling(bottomRight.X));
        int endY = (int)Math.Min(Grid.Height, Math.Ceiling(bottomRight.Y));

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                var cellColor = Grid.Grid[y, x];
                if (cellColor.A > 0) // Draw only if not fully transparent
                {
                    var color = Color.FromArgb(cellColor.A, cellColor.R, cellColor.G, cellColor.B);
                    ds.FillRectangle(x, y, 1, 1, color);
                }
            }
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
        {
            var properties = e.GetCurrentPoint(this);
            if (properties.Properties.IsLeftButtonPressed)
            {
                _lastPointerPosition = properties.Position;
                CapturePointer(e.Pointer);
            }
        }
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

        var gridCenterWorld = new Vector2(Grid.Width / 2.0f, Grid.Height / 2.0f);

        // Position of the grid center on screen before zoom
        var centerPosBefore = Vector2.Transform(gridCenterWorld, _transform);

        // Apply the new scale
        _transform.M11 = newZoom;
        _transform.M22 = newZoom;

        // Position of the grid center on screen after zoom
        var centerPosAfter = Vector2.Transform(gridCenterWorld, _transform);

        // Calculate the translation error introduced by scaling and correct for it
        var error = centerPosBefore - centerPosAfter;
        _transform.Translation += error;

        // After zooming and correcting, clamp the final translation to keep the grid in view
        _transform.Translation = ClampTranslation(_transform.Translation);

        Canvas.Invalidate();
    }

    private Vector2 ClampTranslation(Vector2 proposedTranslation)
    {
        if (Grid is null) return proposedTranslation;

        var canvasWidth = (float)Canvas.ActualWidth;
        var canvasHeight = (float)Canvas.ActualHeight;
        var scale = _transform.M11; // Assuming uniform scale

        var gridScreenWidth = Grid.Width * scale;
        var gridScreenHeight = Grid.Height * scale;

        float minX, maxX, minY, maxY;

        // Determine horizontal clamping range based on whether the grid is wider or narrower than the canvas
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

        // Determine vertical clamping range
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

        // Clamp the proposed translation to the calculated min/max ranges
        return new Vector2(
            Math.Clamp(proposedTranslation.X, minX, maxX),
            Math.Clamp(proposedTranslation.Y, minY, maxY)
        );
    }
}