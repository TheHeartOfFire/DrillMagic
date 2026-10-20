using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DrillMagic.Core.Editing;

/// <summary>
/// Implements basic doctoring operations (crop, freehand painting, lines, rectangles, ellipses).
/// </summary>
public class ImageDoctorService : IImageDoctorService
{
    public void Crop(Image<Rgba32> image, Rectangle cropRectangle)
    {
        ArgumentNullException.ThrowIfNull(image);

        // Clamp crop area to image bounds
        int x = Math.Clamp(cropRectangle.X, 0, image.Width - 1);
        int y = Math.Clamp(cropRectangle.Y, 0, image.Height - 1);
        int width = Math.Clamp(cropRectangle.Width, 1, image.Width - x);
        int height = Math.Clamp(cropRectangle.Height, 1, image.Height - y);

        var clamped = new Rectangle(x, y, width, height);
        image.Mutate(ctx => ctx.Crop(clamped));
    }

    public void DrawLine(Image<Rgba32> image, Point start, Point end, Rgba32 color, float thickness)
    {
        ArgumentNullException.ThrowIfNull(image);
        float actualThickness = Math.Max(1f, thickness);

        image.Mutate(ctx =>
        {
            var pen = new SolidPen(color, actualThickness);
            ctx.DrawLine(pen, new PointF(start.X, start.Y), new PointF(end.X, end.Y));
        });
    }

    public void DrawRectangle(Image<Rgba32> image, Rectangle rectangle, Rgba32 color, float thickness, bool filled)
    {
        ArgumentNullException.ThrowIfNull(image);

        // Normalize rectangle if negative width/height
        int rx = rectangle.Width < 0 ? rectangle.X + rectangle.Width : rectangle.X;
        int ry = rectangle.Height < 0 ? rectangle.Y + rectangle.Height : rectangle.Y;
        int rw = Math.Abs(rectangle.Width);
        int rh = Math.Abs(rectangle.Height);

        if (rw == 0 || rh == 0) return;

        var rectF = new RectangularPolygon(rx, ry, rw, rh);

        image.Mutate(ctx =>
        {
            if (filled)
            {
                ctx.Fill(color, rectF);
            }
            else
            {
                float penThickness = Math.Max(1f, thickness);
                ctx.Draw(new SolidPen(color, penThickness), rectF);
            }
        });
    }

    public void DrawEllipse(Image<Rgba32> image, Rectangle bounds, Rgba32 color, float thickness, bool filled)
    {
        ArgumentNullException.ThrowIfNull(image);

        int ex = bounds.Width < 0 ? bounds.X + bounds.Width : bounds.X;
        int ey = bounds.Height < 0 ? bounds.Y + bounds.Height : bounds.Y;
        int ew = Math.Abs(bounds.Width);
        int eh = Math.Abs(bounds.Height);

        if (ew == 0 || eh == 0) return;

        float centerX = ex + ew / 2f;
        float centerY = ey + eh / 2f;
        var ellipse = new EllipsePolygon(centerX, centerY, ew / 2f, eh / 2f);

        image.Mutate(ctx =>
        {
            if (filled)
            {
                ctx.Fill(color, ellipse);
            }
            else
            {
                float penThickness = Math.Max(1f, thickness);
                ctx.Draw(new SolidPen(color, penThickness), ellipse);
            }
        });
    }

    public void PaintBrush(Image<Rgba32> image, Point center, Rgba32 color, float radius)
    {
        ArgumentNullException.ThrowIfNull(image);

        float r = Math.Max(0.5f, radius);
        if (r <= 1f)
        {
            if (center.X >= 0 && center.X < image.Width && center.Y >= 0 && center.Y < image.Height)
            {
                image[center.X, center.Y] = color;
            }
            return;
        }

        var circle = new EllipsePolygon(center.X, center.Y, r, r);
        image.Mutate(ctx => ctx.Fill(color, circle));
    }

    public Image<Rgba32> CloneImage(Image<Rgba32> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Clone();
    }

    public MemoryStream SaveToPngStream(Image<Rgba32> image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var ms = new MemoryStream();
        image.SaveAsPng(ms);
        ms.Position = 0;
        return ms;
    }
}

