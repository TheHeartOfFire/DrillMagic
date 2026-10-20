using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DrillMagic.Core.Editing;

/// <summary>
/// Defines image doctoring and pre-generation manipulation operations.
/// </summary>
public interface IImageDoctorService
{
    /// <summary>
    /// Crops the image to the specified bounding rectangle.
    /// </summary>
    void Crop(Image<Rgba32> image, Rectangle cropRectangle);

    /// <summary>
    /// Draws a straight line or smooth segment between two points with the specified color and thickness.
    /// </summary>
    void DrawLine(Image<Rgba32> image, Point start, Point end, Rgba32 color, float thickness);

    /// <summary>
    /// Draws a rectangle on the image.
    /// </summary>
    void DrawRectangle(Image<Rgba32> image, Rectangle rectangle, Rgba32 color, float thickness, bool filled);

    /// <summary>
    /// Draws an ellipse on the image.
    /// </summary>
    void DrawEllipse(Image<Rgba32> image, Rectangle bounds, Rgba32 color, float thickness, bool filled);

    /// <summary>
    /// Fills a circular brush spot at the given point.
    /// </summary>
    void PaintBrush(Image<Rgba32> image, Point center, Rgba32 color, float radius);

    /// <summary>
    /// Clones an image.
    /// </summary>
    Image<Rgba32> CloneImage(Image<Rgba32> source);

    /// <summary>
    /// Encodes an image to PNG memory stream.
    /// </summary>
    MemoryStream SaveToPngStream(Image<Rgba32> image);
}

