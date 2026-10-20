using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DrillMagic.Core.Export;

public class ImageSharpGridExporter : IGridImageExportService
{
    private readonly ILogger<ImageSharpGridExporter> _logger;

    public ImageSharpGridExporter(ILogger<ImageSharpGridExporter>? logger = null)
    {
        _logger = logger ?? NullLogger<ImageSharpGridExporter>.Instance;
    }

    public async Task<byte[]> ExportToPngAsync(DrillGrid grid, ImageExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grid);
        options ??= new ImageExportOptions();

        int pixelsPerCell = Math.Clamp(options.PixelsPerCell, 1, 100);
        bool includeGridLines = options.IncludeGridLines && pixelsPerCell >= 4;

        int gridWidth = grid.Width;
        int gridHeight = grid.Height;

        if (gridWidth <= 0 || gridHeight <= 0)
        {
            throw new InvalidOperationException("Cannot export an empty grid.");
        }

        int imgWidth = gridWidth * pixelsPerCell;
        int imgHeight = gridHeight * pixelsPerCell;

        _logger.LogInformation("Exporting grid {GridWidth}x{GridHeight} to PNG ({ImgWidth}x{ImgHeight} px, {Scale} px/cell)",
            gridWidth, gridHeight, imgWidth, imgHeight, pixelsPerCell);

        using var image = new Image<Rgba32>(imgWidth, imgHeight);

        var gridLineColor = new Rgba32(60, 60, 60, 100);

        for (int gy = 0; gy < gridHeight; gy++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int gx = 0; gx < gridWidth; gx++)
            {
                var sysColor = grid.Grid[gy, gx];
                var cellPixelColor = new Rgba32(sysColor.R, sysColor.G, sysColor.B, sysColor.A);

                int startX = gx * pixelsPerCell;
                int startY = gy * pixelsPerCell;

                for (int py = 0; py < pixelsPerCell; py++)
                {
                    int currentY = startY + py;
                    for (int px = 0; px < pixelsPerCell; px++)
                    {
                        int currentX = startX + px;

                        if (includeGridLines && (px == pixelsPerCell - 1 || py == pixelsPerCell - 1))
                        {
                            image[currentX, currentY] = gridLineColor;
                        }
                        else
                        {
                            image[currentX, currentY] = cellPixelColor;
                        }
                    }
                }
            }
        }

        using var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms, cancellationToken);
        _logger.LogInformation("PNG export complete. Output size: {ByteCount} bytes", ms.Length);
        return ms.ToArray();
    }
}

