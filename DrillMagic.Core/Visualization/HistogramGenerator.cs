using System;
using System.Linq;
using DrillMagic.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using DrawingColor = System.Drawing.Color;
using ImageSharpColor = SixLabors.ImageSharp.Color;

namespace DrillMagic.Core.Visualization;

public class HistogramGenerator
{
    private const int DefaultWidth = 800;
    private const int DefaultHeight = 400;

    public Image<Rgba32> GenerateHistogram(DrillGrid grid, int width = DefaultWidth, int height = DefaultHeight)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (grid.ColorSummary == null) throw new ArgumentException("Grid has no color summary.");

        var image = new Image<Rgba32>(width, height);

        // Fill background
        image.Mutate(ctx => ctx.Fill(ImageSharpColor.White));

        if (grid.ColorSummary.Count == 0)
        {
            return image;
        }

        // Sort colors by count descending
        var sortedColors = grid.ColorSummary
            .OrderByDescending(x => x.Value)
            .ToList();

        int count = sortedColors.Count;
        int maxVal = sortedColors.First().Value;

        // Calculate bar dimensions
        // Reserve some margin
        float margin = 20f;
        float chartWidth = width - 2 * margin;
        float chartHeight = height - 2 * margin;
        
        float barWidth = chartWidth / count;
        
        image.Mutate(ctx =>
        {
            for (int i = 0; i < count; i++)
            {
                var item = sortedColors[i];
                var sysColor = item.Key;
                // Convert System.Drawing.Color to ImageSharp.Color
                var color = ImageSharpColor.FromRgba(sysColor.R, sysColor.G, sysColor.B, sysColor.A);

                float barHeight = (float)item.Value / maxVal * chartHeight;
                float x = margin + i * barWidth;
                float y = height - margin - barHeight;

                // Draw bar rect
                var rect = new RectangleF(x, y, barWidth, barHeight);
                ctx.Fill(color, rect);
                
                // Draw border for visibility if color is white/light
                ctx.Draw(ImageSharpColor.Black, 1, rect);
            }
        });

        return image;
    }
}
