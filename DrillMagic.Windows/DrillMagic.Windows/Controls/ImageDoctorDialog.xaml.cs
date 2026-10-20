using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using DrillMagic.Core.Editing;
using DrillMagic.Core.Projects;
using DrillMagic.Core.Sizing;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Windows.Foundation;
using Windows.Graphics.DirectX;

namespace DrillMagic.Windows.Controls;

public sealed partial class ImageDoctorDialog : ContentDialog
{
    private enum DoctorTool
    {
        Pan,
        Crop,
        Brush,
        Line,
        Rectangle,
        Ellipse,
        Eyedropper
    }

    private readonly IImageDoctorService _doctorService = new ImageDoctorService();

    private Image<Rgba32>? _originalImage;
    private Image<Rgba32>? _workingImage;
    private CanvasBitmap? _canvasBitmap;
    private readonly Stack<Image<Rgba32>> _undoStack = new();

    private DoctorTool _currentTool = DoctorTool.Pan;
    private Rgba32 _activeColor = new(0, 0, 0, 255);
    private float _brushSize = 4f;
    private bool _fillShape = true;
    private DrillShape _drillShape = DrillShape.Square;

    // Viewport & interaction state
    private Matrix3x2 _transform = Matrix3x2.Identity;
    private bool _isCentered = false;
    private bool _hasUserPannedOrZoomed = false;
    private global::Windows.Foundation.Point? _panStartPointer;
    private Vector2 _panStartTranslation;

    private SixLabors.ImageSharp.Point? _dragStartImage;
    private SixLabors.ImageSharp.Point? _dragCurrentImage;
    private SixLabors.ImageSharp.Point? _lastBrushImage;
    private SixLabors.ImageSharp.Rectangle? _activeCropBox;

    public ImageDoctorDialog()
    {
        InitializeComponent();
        this.Closing += ImageDoctorDialog_Closing;
    }

    /// <summary>
    /// Displays the doctoring dialog for an image stream.
    /// </summary>
    public static async System.Threading.Tasks.Task<(bool Apply, MemoryStream? ResultStream)> ShowForImageAsync(
        Stream imageStream,
        XamlRoot xamlRoot,
        DrillShape drillShape = DrillShape.Square,
        IEnumerable<SourceImageVersionSummary>? imageHistory = null,
        IReadOnlyDictionary<string, byte[]>? imageVersionsData = null)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        var dialog = new ImageDoctorDialog
        {
            XamlRoot = xamlRoot,
            _drillShape = drillShape
        };

        if (imageHistory != null && imageVersionsData != null)
        {
            var historyList = imageHistory.ToList();
            if (historyList.Count > 0)
            {
                dialog.HistoryBtn.Visibility = Visibility.Visible;
                dialog.HistoryMenuFlyout.Items.Clear();
                foreach (var version in historyList)
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = $"{version.Name} ({version.SummaryText})",
                        Tag = version.Id
                    };
                    item.Click += (s, e) =>
                    {
                        if (s is MenuFlyoutItem mfi && mfi.Tag is string vid && imageVersionsData.TryGetValue(vid, out var bytes) && bytes != null && bytes.Length > 0)
                        {
                            try
                            {
                                using var vms = new MemoryStream(bytes);
                                var loaded = SixLabors.ImageSharp.Image.Load<Rgba32>(vms);
                                dialog.SwitchToVersionImage(loaded);
                            }
                            catch
                            {
                                // Ignore version loading error
                            }
                        }
                    };
                    dialog.HistoryMenuFlyout.Items.Add(item);
                }
            }
        }

        imageStream.Position = 0;
        dialog._originalImage = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(imageStream);
        dialog._workingImage = dialog._originalImage.Clone();
        dialog.UpdateDimensionsReadout();

        // Expand dialog generously to occupy 95% of the window width and 90% of window height
        double windowW = xamlRoot.Size.Width > 0 ? xamlRoot.Size.Width : 1400;
        double windowH = xamlRoot.Size.Height > 0 ? xamlRoot.Size.Height : 900;

        double targetW = Math.Max(950, windowW * 0.95);
        double targetH = Math.Max(650, windowH * 0.90);

        dialog.MaxWidth = Math.Max(targetW + 100, 5000);
        dialog.MaxHeight = Math.Max(targetH + 100, 3000);

        dialog.RootContainer.Width = targetW;
        dialog.RootContainer.Height = targetH;

        var dialogResult = await dialog.ShowAsync();

        if (dialogResult == ContentDialogResult.Primary && dialog._workingImage != null)
        {
            dialog.CommitActiveEdits();
            // Apply & Generate doctored image
            var result = dialog._doctorService.SaveToPngStream(dialog._workingImage);
            dialog.DisposeResources();
            return (true, result);
        }
        else if (dialogResult == ContentDialogResult.Secondary)
        {
            // Skip edits & Generate with original
            dialog.DisposeResources();
            imageStream.Position = 0;
            var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms);
            ms.Position = 0;
            return (true, ms);
        }

        // Cancel
        dialog.DisposeResources();
        return (false, null);
    }

    /// <summary>
    /// Commits any pending edits (e.g. active crop box or in-flight shape drags) into the working image.
    /// </summary>
    public void CommitActiveEdits()
    {
        if (_workingImage == null) return;

        // 1. Commit active crop box
        if (_activeCropBox.HasValue)
        {
            PushUndo();
            _doctorService.Crop(_workingImage, _activeCropBox.Value);
            _activeCropBox = null;
            if (ApplyCropBtn != null) ApplyCropBtn.Visibility = Visibility.Collapsed;
            if (ResetCropBtn != null) ResetCropBtn.Visibility = Visibility.Collapsed;
            _hasUserPannedOrZoomed = false;
            _isCentered = false;
        }

        // 2. Commit in-flight shape drag if pointer wasn't explicitly released before clicking Apply
        if (_dragStartImage.HasValue && _dragCurrentImage.HasValue)
        {
            var p1 = _dragStartImage.Value;
            var p2 = _dragCurrentImage.Value;
            if (_currentTool == DoctorTool.Line)
            {
                PushUndo();
                _doctorService.DrawLine(_workingImage, p1, p2, _activeColor, _brushSize);
            }
            else if (_currentTool == DoctorTool.Rectangle)
            {
                PushUndo();
                var r = GetNormalizedRect(p1, p2);
                _doctorService.DrawRectangle(_workingImage, r, _activeColor, _brushSize, _fillShape);
            }
            else if (_currentTool == DoctorTool.Ellipse)
            {
                PushUndo();
                var r = GetNormalizedRect(p1, p2);
                _doctorService.DrawEllipse(_workingImage, r, _activeColor, _brushSize, _fillShape);
            }
            else if (_currentTool == DoctorTool.Crop)
            {
                PushUndo();
                var r = GetNormalizedRect(p1, p2);
                _doctorService.Crop(_workingImage, r);
                _hasUserPannedOrZoomed = false;
                _isCentered = false;
            }
            _dragStartImage = null;
            _dragCurrentImage = null;
        }

        if (_lastBrushImage.HasValue)
        {
            _lastBrushImage = null;
        }
    }

    private void ImageDoctorDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        CommitActiveEdits();
    }

    private void DoctorCanvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_currentTool == DoctorTool.Crop && _activeCropBox.HasValue)
        {
            ApplyCropBtn_Click(sender, null!);
        }
    }

    private void RootContainer_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter && _activeCropBox.HasValue)
        {
            ApplyCropBtn_Click(sender, null!);
            e.Handled = true;
        }
        else if (e.Key == global::Windows.System.VirtualKey.Escape && _activeCropBox.HasValue)
        {
            ResetCropBtn_Click(sender, null!);
            e.Handled = true;
        }
    }

    private void DisposeResources()
    {
        _canvasBitmap?.Dispose();
        _canvasBitmap = null;
        _workingImage?.Dispose();
        _workingImage = null;
        _originalImage?.Dispose();
        _originalImage = null;

        while (_undoStack.Count > 0)
        {
            _undoStack.Pop().Dispose();
        }
    }

    private void ImageDoctorDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        // Ensure cleanup if closed through close button or esc
        if (args.Result == ContentDialogResult.None)
        {
            DisposeResources();
        }
    }

    private void UpdateDimensionsReadout()
    {
        if (_workingImage == null) return;

        ImageDimensionsText.Text = $"Image: {_workingImage.Width} × {_workingImage.Height} px";

        var (projW, projH) = PhysicalDimensionsCalculator.CalculateProjectedGridSize(_workingImage.Width, _workingImage.Height, 10);
        var scaleStr = PhysicalDimensionsCalculator.FormatDimensions(projW, projH, _drillShape);
        ProjectedScaleText.Text = $"{projW} × {projH} Drills • {scaleStr}";

        if (_activeCropBox.HasValue)
        {
            var b = _activeCropBox.Value;
            CropInfoText.Visibility = Visibility.Visible;
            CropInfoText.Text = $"Crop Box: {Math.Abs(b.Width)} × {Math.Abs(b.Height)} px (Double-click, Enter, or Apply)";
        }
        else
        {
            CropInfoText.Visibility = Visibility.Collapsed;
        }
    }

    private void PushUndo()
    {
        if (_workingImage == null) return;
        _undoStack.Push(_doctorService.CloneImage(_workingImage));
        UndoBtn.IsEnabled = true;
    }

    private void SyncBitmapFromWorkingImage(ICanvasResourceCreator resourceCreator)
    {
        if (_workingImage == null) return;

        _canvasBitmap?.Dispose();

        byte[] pixels = new byte[_workingImage.Width * _workingImage.Height * 4];
        _workingImage.CopyPixelDataTo(pixels);

        _canvasBitmap = CanvasBitmap.CreateFromBytes(
            resourceCreator,
            pixels,
            _workingImage.Width,
            _workingImage.Height,
            DirectXPixelFormat.R8G8B8A8UIntNormalized);
    }

    // ==================== DRAWING VIEWPORT ====================

    private void DoctorCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (_workingImage == null) return;

        var ds = args.DrawingSession;

        if (_canvasBitmap == null)
        {
            SyncBitmapFromWorkingImage(sender);
        }

        // Center and fit on first draw or when image size changes
        if (!_isCentered && sender.ActualWidth > 10 && sender.ActualHeight > 10)
        {
            FitToViewport(sender.ActualWidth, sender.ActualHeight);
        }

        ds.Transform = _transform;

        // Draw image
        if (_canvasBitmap != null)
        {
            ds.DrawImage(_canvasBitmap, new Rect(0, 0, _workingImage.Width, _workingImage.Height));
        }

        // Live Shape Preview while dragging
        if (_dragStartImage.HasValue && _dragCurrentImage.HasValue)
        {
            var p1 = _dragStartImage.Value;
            var p2 = _dragCurrentImage.Value;
            var previewColor = global::Windows.UI.Color.FromArgb(_activeColor.A, _activeColor.R, _activeColor.G, _activeColor.B);

            if (_currentTool == DoctorTool.Line)
            {
                ds.DrawLine(p1.X, p1.Y, p2.X, p2.Y, previewColor, _brushSize);
            }
            else if (_currentTool == DoctorTool.Rectangle)
            {
                var r = GetNormalizedRect(p1, p2);
                if (_fillShape)
                {
                    ds.FillRectangle(r.X, r.Y, r.Width, r.Height, previewColor);
                }
                else
                {
                    ds.DrawRectangle(r.X, r.Y, r.Width, r.Height, previewColor, _brushSize);
                }
            }
            else if (_currentTool == DoctorTool.Ellipse)
            {
                var r = GetNormalizedRect(p1, p2);
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                float rx = r.Width / 2f;
                float ry = r.Height / 2f;
                if (_fillShape)
                {
                    ds.FillEllipse(cx, cy, rx, ry, previewColor);
                }
                else
                {
                    ds.DrawEllipse(cx, cy, rx, ry, previewColor, _brushSize);
                }
            }
            else if (_currentTool == DoctorTool.Crop)
            {
                var crop = GetNormalizedRect(p1, p2);
                _activeCropBox = crop;

                // Darken outside crop
                var strokeColor = Microsoft.UI.Colors.Cyan;
                ds.DrawRectangle(crop.X, crop.Y, crop.Width, crop.Height, strokeColor, 2f / _transform.M11);
            }
        }
        else if (_activeCropBox.HasValue && _currentTool == DoctorTool.Crop)
        {
            var crop = _activeCropBox.Value;
            ds.DrawRectangle(crop.X, crop.Y, crop.Width, crop.Height, Microsoft.UI.Colors.Cyan, 2f / _transform.M11);
        }
    }

    private static SixLabors.ImageSharp.Rectangle GetNormalizedRect(
        SixLabors.ImageSharp.Point p1, SixLabors.ImageSharp.Point p2)
    {
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Max(1, Math.Abs(p1.X - p2.X));
        int h = Math.Max(1, Math.Abs(p1.Y - p2.Y));
        return new SixLabors.ImageSharp.Rectangle(x, y, w, h);
    }

    // ==================== VIEWPORT CONTROLS ====================

    private void FitToViewport(double viewportW, double viewportH)
    {
        if (_workingImage == null || viewportW <= 0 || viewportH <= 0) return;

        float padding = 20f;
        float availW = Math.Max(10f, (float)viewportW - padding * 2);
        float availH = Math.Max(10f, (float)viewportH - padding * 2);

        float scaleX = availW / _workingImage.Width;
        float scaleY = availH / _workingImage.Height;
        float scale = Math.Min(scaleX, scaleY);

        if (scale > 4f) scale = 4f;

        float tx = ((float)viewportW - _workingImage.Width * scale) / 2f;
        float ty = ((float)viewportH - _workingImage.Height * scale) / 2f;

        _transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(tx, ty);
        _isCentered = true;
    }

    private void DoctorCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_hasUserPannedOrZoomed && e.NewSize.Width > 50 && e.NewSize.Height > 50)
        {
            FitToViewport(e.NewSize.Width, e.NewSize.Height);
            DoctorCanvas.Invalidate();
        }
    }

    private void FitViewBtn_Click(object sender, RoutedEventArgs e)
    {
        _hasUserPannedOrZoomed = false;
        FitToViewport(DoctorCanvas.ActualWidth, DoctorCanvas.ActualHeight);
        DoctorCanvas.Invalidate();
    }

    private void ActualSizeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_workingImage == null) return;
        _hasUserPannedOrZoomed = true;
        float tx = ((float)DoctorCanvas.ActualWidth - _workingImage.Width) / 2f;
        float ty = ((float)DoctorCanvas.ActualHeight - _workingImage.Height) / 2f;
        _transform = Matrix3x2.CreateTranslation(tx, ty);
        _isCentered = true;
        DoctorCanvas.Invalidate();
    }

    // ==================== POINTER EVENTS ====================

    private SixLabors.ImageSharp.Point? CanvasToImagePoint(global::Windows.Foundation.Point canvasPoint)
    {
        if (_workingImage == null) return null;

        if (Matrix3x2.Invert(_transform, out var inv))
        {
            var p = Vector2.Transform(new Vector2((float)canvasPoint.X, (float)canvasPoint.Y), inv);
            int x = (int)Math.Round(p.X);
            int y = (int)Math.Round(p.Y);
            return new SixLabors.ImageSharp.Point(x, y);
        }
        return null;
    }

    private void DoctorCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_workingImage == null) return;

        var pt = e.GetCurrentPoint(DoctorCanvas);
        var imgPt = CanvasToImagePoint(pt.Position);

        if (_currentTool == DoctorTool.Pan || pt.Properties.IsMiddleButtonPressed)
        {
            _panStartPointer = pt.Position;
            _panStartTranslation = _transform.Translation;
            DoctorCanvas.CapturePointer(e.Pointer);
            return;
        }

        if (!pt.Properties.IsLeftButtonPressed || !imgPt.HasValue) return;

        var p = imgPt.Value;

        if (_currentTool == DoctorTool.Eyedropper)
        {
            if (p.X >= 0 && p.X < _workingImage.Width && p.Y >= 0 && p.Y < _workingImage.Height)
            {
                var c = _workingImage[p.X, p.Y];
                SetActiveColor(c);
            }
            return;
        }

        if (_currentTool == DoctorTool.Brush)
        {
            PushUndo();
            _lastBrushImage = p;
            _doctorService.PaintBrush(_workingImage, p, _activeColor, _brushSize / 2f);
            SyncBitmapFromWorkingImage(DoctorCanvas);
            DoctorCanvas.Invalidate();
            DoctorCanvas.CapturePointer(e.Pointer);
            return;
        }

        if (_currentTool == DoctorTool.Crop || _currentTool == DoctorTool.Line ||
            _currentTool == DoctorTool.Rectangle || _currentTool == DoctorTool.Ellipse)
        {
            _dragStartImage = p;
            _dragCurrentImage = p;
            DoctorCanvas.CapturePointer(e.Pointer);
            DoctorCanvas.Invalidate();
        }
    }

    private void DoctorCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_workingImage == null) return;

        var pt = e.GetCurrentPoint(DoctorCanvas);

        if (_panStartPointer.HasValue)
        {
            var delta = new Vector2((float)(pt.Position.X - _panStartPointer.Value.X), (float)(pt.Position.Y - _panStartPointer.Value.Y));
            _transform.Translation = _panStartTranslation + delta;
            _hasUserPannedOrZoomed = true;
            DoctorCanvas.Invalidate();
            return;
        }

        var imgPt = CanvasToImagePoint(pt.Position);
        if (!imgPt.HasValue) return;
        var p = imgPt.Value;

        if (_currentTool == DoctorTool.Brush && _lastBrushImage.HasValue)
        {
            _doctorService.DrawLine(_workingImage, _lastBrushImage.Value, p, _activeColor, _brushSize);
            _doctorService.PaintBrush(_workingImage, p, _activeColor, _brushSize / 2f);
            _lastBrushImage = p;
            SyncBitmapFromWorkingImage(DoctorCanvas);
            DoctorCanvas.Invalidate();
            return;
        }

        if (_dragStartImage.HasValue)
        {
            _dragCurrentImage = p;
            UpdateDimensionsReadout();
            DoctorCanvas.Invalidate();
        }
    }

    private void DoctorCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_panStartPointer.HasValue)
        {
            _panStartPointer = null;
            DoctorCanvas.ReleasePointerCapture(e.Pointer);
            return;
        }

        if (_lastBrushImage.HasValue)
        {
            _lastBrushImage = null;
            DoctorCanvas.ReleasePointerCapture(e.Pointer);
            return;
        }

        if (_dragStartImage.HasValue && _dragCurrentImage.HasValue && _workingImage != null)
        {
            var p1 = _dragStartImage.Value;
            var p2 = _dragCurrentImage.Value;

            if (_currentTool == DoctorTool.Line)
            {
                PushUndo();
                _doctorService.DrawLine(_workingImage, p1, p2, _activeColor, _brushSize);
                SyncBitmapFromWorkingImage(DoctorCanvas);
            }
            else if (_currentTool == DoctorTool.Rectangle)
            {
                PushUndo();
                var r = GetNormalizedRect(p1, p2);
                _doctorService.DrawRectangle(_workingImage, r, _activeColor, _brushSize, _fillShape);
                SyncBitmapFromWorkingImage(DoctorCanvas);
            }
            else if (_currentTool == DoctorTool.Ellipse)
            {
                PushUndo();
                var r = GetNormalizedRect(p1, p2);
                _doctorService.DrawEllipse(_workingImage, r, _activeColor, _brushSize, _fillShape);
                SyncBitmapFromWorkingImage(DoctorCanvas);
            }
            else if (_currentTool == DoctorTool.Crop)
            {
                var r = GetNormalizedRect(p1, p2);
                _activeCropBox = r;
                ApplyCropBtn.Visibility = Visibility.Visible;
                ResetCropBtn.Visibility = Visibility.Visible;
            }

            _dragStartImage = null;
            _dragCurrentImage = null;
            DoctorCanvas.ReleasePointerCapture(e.Pointer);
            UpdateDimensionsReadout();
            DoctorCanvas.Invalidate();
        }
    }

    private void DoctorCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(DoctorCanvas).Properties.MouseWheelDelta;
        float scaleFactor = delta > 0 ? 1.15f : 1 / 1.15f;

        var pos = e.GetCurrentPoint(DoctorCanvas).Position;
        var pointerVec = new Vector2((float)pos.X, (float)pos.Y);

        _transform = _transform * Matrix3x2.CreateScale(scaleFactor, pointerVec);
        _hasUserPannedOrZoomed = true;
        DoctorCanvas.Invalidate();
    }

    // ==================== TOOL SELECTION & OPTIONS ====================

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        ToolPanBtn.IsChecked = sender == ToolPanBtn;
        ToolCropBtn.IsChecked = sender == ToolCropBtn;
        ToolBrushBtn.IsChecked = sender == ToolBrushBtn;
        ToolLineBtn.IsChecked = sender == ToolLineBtn;
        ToolRectBtn.IsChecked = sender == ToolRectBtn;
        ToolEllipseBtn.IsChecked = sender == ToolEllipseBtn;
        ToolEyedropperBtn.IsChecked = sender == ToolEyedropperBtn;

        if (sender == ToolPanBtn) _currentTool = DoctorTool.Pan;
        else if (sender == ToolCropBtn) _currentTool = DoctorTool.Crop;
        else if (sender == ToolBrushBtn) _currentTool = DoctorTool.Brush;
        else if (sender == ToolLineBtn) _currentTool = DoctorTool.Line;
        else if (sender == ToolRectBtn) _currentTool = DoctorTool.Rectangle;
        else if (sender == ToolEllipseBtn) _currentTool = DoctorTool.Ellipse;
        else if (sender == ToolEyedropperBtn) _currentTool = DoctorTool.Eyedropper;

        // Visibility of contextual options
        FillShapeCheckBox.Visibility = (_currentTool == DoctorTool.Rectangle || _currentTool == DoctorTool.Ellipse)
            ? Visibility.Visible : Visibility.Collapsed;

        ApplyCropBtn.Visibility = (_currentTool == DoctorTool.Crop && _activeCropBox.HasValue)
            ? Visibility.Visible : Visibility.Collapsed;
        ResetCropBtn.Visibility = (_currentTool == DoctorTool.Crop && _activeCropBox.HasValue)
            ? Visibility.Visible : Visibility.Collapsed;

        DoctorCanvas.Invalidate();
    }

    private void BrushSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _brushSize = (float)e.NewValue;
        if (BrushSizeText != null)
        {
            BrushSizeText.Text = $"{(int)_brushSize} px";
        }
    }

    private void DoctorColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs e)
    {
        var c = e.NewColor;
        _activeColor = new Rgba32(c.R, c.G, c.B, c.A);
        ColorPreviewBorder.Background = new SolidColorBrush(c);
    }

    private void SetActiveColor(Rgba32 c)
    {
        _activeColor = c;
        var winColor = global::Windows.UI.Color.FromArgb(c.A, c.R, c.G, c.B);
        ColorPreviewBorder.Background = new SolidColorBrush(winColor);
        DoctorColorPicker.Color = winColor;
    }

    private void DoctorColorSwatchBtn_Click(object sender, RoutedEventArgs e)
    {
        ColorPickerFlyout.ShowAt(DoctorColorSwatchBtn);
    }

    // ==================== CROP ACTIONS ====================

    private void ApplyCropBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_workingImage == null || !_activeCropBox.HasValue) return;

        PushUndo();
        _doctorService.Crop(_workingImage, _activeCropBox.Value);
        _activeCropBox = null;
        ApplyCropBtn.Visibility = Visibility.Collapsed;
        ResetCropBtn.Visibility = Visibility.Collapsed;

        // Reset centering so the cropped image re-centers and fits
        _hasUserPannedOrZoomed = false;
        _isCentered = false;
        SyncBitmapFromWorkingImage(DoctorCanvas);
        UpdateDimensionsReadout();
        DoctorCanvas.Invalidate();
    }

    private void ResetCropBtn_Click(object sender, RoutedEventArgs e)
    {
        _activeCropBox = null;
        ApplyCropBtn.Visibility = Visibility.Collapsed;
        ResetCropBtn.Visibility = Visibility.Collapsed;
        UpdateDimensionsReadout();
        DoctorCanvas.Invalidate();
    }

    // ==================== UNDO & REVERT ====================

    private void UndoBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count == 0) return;

        _workingImage?.Dispose();
        _workingImage = _undoStack.Pop();
        _activeCropBox = null;

        UndoBtn.IsEnabled = _undoStack.Count > 0;
        _hasUserPannedOrZoomed = false;
        _isCentered = false;
        SyncBitmapFromWorkingImage(DoctorCanvas);
        UpdateDimensionsReadout();
        DoctorCanvas.Invalidate();
    }

    private void RevertBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_originalImage == null) return;

        PushUndo();
        _workingImage?.Dispose();
        _workingImage = _originalImage.Clone();
        _activeCropBox = null;

        _hasUserPannedOrZoomed = false;
        _isCentered = false;
        SyncBitmapFromWorkingImage(DoctorCanvas);
        UpdateDimensionsReadout();
        DoctorCanvas.Invalidate();
    }

    public void SwitchToVersionImage(SixLabors.ImageSharp.Image<Rgba32> newImage)
    {
        ArgumentNullException.ThrowIfNull(newImage);
        PushUndo();
        _originalImage?.Dispose();
        _originalImage = newImage;
        _workingImage?.Dispose();
        _workingImage = _originalImage.Clone();
        _activeCropBox = null;
        _hasUserPannedOrZoomed = false;
        _isCentered = false;
        SyncBitmapFromWorkingImage(DoctorCanvas);
        UpdateDimensionsReadout();
        DoctorCanvas.Invalidate();
    }
}
