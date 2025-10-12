using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Controls;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;

namespace DrillMagic.Windows.Utils;

public static class PdfGenerator
{
    private const float DrillWidth = 0.11811f; // Approx 3mm
    private const float DrillWidthPoints = DrillWidth * 72; // 1 inch = 72 points

    static PdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static async Task<(byte[]? PdfBytes, string? ErrorMessage)> GenerateDrillGridPdf(DrillGrid grid)
    {
        try
        {
            var symbols = new Dictionary<System.Drawing.Color, string>();
            int currentSymbolIndex = 0;
            foreach (var color in grid.ColorSummary.OrderByDescending(kvp => kvp.Value))
            {
                symbols[color.Key] = ColorSummaryLegend.Symbols[currentSymbolIndex].ToString();
                currentSymbolIndex++;
            }

            // Use the full page size in points
            const float availableWidthPoints = 8.5f * 72;
            const float availableHeightPoints = 11f * 72;

            var cellsPerPageX = (int)(availableWidthPoints / DrillWidthPoints);
            var cellsPerPageY = (int)(availableHeightPoints / DrillWidthPoints);

            var totalPagesX = (grid.Width + cellsPerPageX - 1) / cellsPerPageX;
            var totalPagesY = (grid.Height + cellsPerPageY - 1) / cellsPerPageY;

            var pageImages = new List<byte[]>();
            for (int pageY = 0; pageY < totalPagesY; pageY++)
            {
                for (int pageX = 0; pageX < totalPagesX; pageX++)
                {
                    var startX = pageX * cellsPerPageX;
                    var startY = pageY * cellsPerPageY;
                    var chunkWidth = Math.Min(cellsPerPageX, grid.Width - startX);
                    var chunkHeight = Math.Min(cellsPerPageY, grid.Height - startY);

                    var chunkRect = new Rect(startX, startY, chunkWidth, chunkHeight);
                    
                    var imageBytes = await GridImageRenderer.RenderGridChunkToPng(grid, symbols, chunkRect, DrillWidthPoints);
                    pageImages.Add(imageBytes);
                }
            }

            var document = Document.Create(container =>
            {
                // 1. Add the Legend page first.
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(36); // Keep a margin for the legend page for readability

                    page.Header()
                        .PaddingBottom(20) // Apply padding to the header's container
                        .Text("Color Legend")
                        .SemiBold().FontSize(20);
                    
                    page.Content().Column(column =>
                    {
                        foreach (var entry in symbols)
                        {
                            column.Item().PaddingBottom(5).Row(row =>
                            {
                                string hexColor = $"#{entry.Key.R:X2}{entry.Key.G:X2}{entry.Key.B:X2}";
                                row.ConstantItem(20).Height(20).Background(hexColor);
                                
                                var dmcColor = ColorMap.GetDMCColor(entry.Key);
                                row.RelativeItem().PaddingLeft(10).Text($"{entry.Value} - {dmcColor.Name} ({dmcColor.DMCNumber})").FontSize(14);
                            });
                        }
                    });
                });

                // 2. Add all the grid pages.
                for (int i = 0; i < pageImages.Count; i++)
                {
                    var imageBytes = pageImages[i];
                    
                    container.Page(page =>
                    {
                        page.Size(PageSizes.Letter);
                        page.Margin(0); // No margins for the grid pages

                        // Use FitArea to ensure the image scales down to fit the page exactly.
                        page.Content().Image(imageBytes).FitArea();
                    });
                }
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return (stream.ToArray(), null);
        }
        catch (Exception ex)
        {
            return (null, ex.ToString());
        }
    }
}