using DrillMagic.Core;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;

namespace DrillMagic.Windows.Utils;

public static class GridImageRenderer
{
    public static async Task<byte[]> RenderGridChunkToPng(DrillGrid grid, Dictionary<System.Drawing.Color, string> symbols, Rect chunk, float cellSizeInPoints)
    {
        var device = CanvasDevice.GetSharedDevice();

        // The size of the image in pixels is the number of cells * cell size in points.
        // We use a DPI of 72 so that 1 point = 1 pixel.
        const float dpi = 72;
        var renderTargetWidth = (float)chunk.Width * cellSizeInPoints;
        var renderTargetHeight = (float)chunk.Height * cellSizeInPoints;

        var renderTarget = new CanvasRenderTarget(device, renderTargetWidth, renderTargetHeight, dpi);

        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Colors.White);

            using var textFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontSize = cellSizeInPoints * 0.6f,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center,
                WordWrapping = CanvasWordWrapping.NoWrap
            };

            for (int y = 0; y < (int)chunk.Height; y++)
            {
                for (int x = 0; x < (int)chunk.Width; x++)
                {
                    var gridY = (int)chunk.Top + y;
                    var gridX = (int)chunk.Left + x;

                    if (gridY >= grid.Height || gridX >= grid.Width) continue;

                    var cellColor = grid.Grid[gridY, gridX];
                    if (cellColor.A > 0)
                    {
                        var uiColor = Color.FromArgb(cellColor.A, cellColor.R, cellColor.G, cellColor.B);
                        var rect = new Rect(x * cellSizeInPoints, y * cellSizeInPoints, cellSizeInPoints, cellSizeInPoints);
                        ds.FillRectangle(rect, uiColor);

                        if (symbols.TryGetValue(cellColor, out var symbol))
                        {
                            var brightness = (0.299 * cellColor.R + 0.587 * cellColor.G + 0.114 * cellColor.B) / 255;
                            var symbolColor = brightness > 0.5 ? Colors.Black : Colors.White;
                            ds.DrawText(symbol, (float)rect.Left + (float)rect.Width / 2, (float)rect.Top + (float)rect.Height / 2, symbolColor, textFormat);
                        }
                    }
                }
            }
        }

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
        
        var bytes = new byte[stream.Size];
        await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
        return bytes;
    }
}