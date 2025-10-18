using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Controls;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DrillMagic.Windows.Utils;

public static class PdfSharpGenerator
{
    private const float DrillWidth = 0.11811f; // Approx 3mm
    private const float DrillWidthPoints = DrillWidth * 72; // 1 inch = 72 points

    /// <summary>
    /// Static constructor to register our custom font resolver with PDFsharp.
    /// This is called once before any other method in this class is accessed.
    /// </summary>
    static PdfSharpGenerator()
    {
        GlobalFontSettings.FontResolver = new FontResolver();
    }

    public static Task<(byte[]? PdfBytes, string? ErrorMessage)> GenerateDrillGridPdf(DrillGrid grid)
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

            using var document = new PdfDocument();
            
            // CRITICAL: Pass the grid's ColorSummary to the legend page method.
            AddLegendPage(document, grid.ColorSummary, symbols);

            // Grid Pages
            const float availableWidthPoints = 8.5f * 72;
            const float availableHeightPoints = 11f * 72;
            var cellsPerPageX = (int)(availableWidthPoints / DrillWidthPoints);
            var cellsPerPageY = (int)(availableHeightPoints / DrillWidthPoints);
            var totalPagesX = (grid.Width + cellsPerPageX - 1) / cellsPerPageX;
            var totalPagesY = (grid.Height + cellsPerPageY - 1) / cellsPerPageY;

            for (int pageY = 0; pageY < totalPagesY; pageY++)
            {
                for (int pageX = 0; pageX < totalPagesX; pageX++)
                {
                    var page = document.AddPage();
                    page.Width = XUnit.FromPoint(availableWidthPoints);
                    page.Height = XUnit.FromPoint(availableHeightPoints);
                    using var gfx = XGraphics.FromPdfPage(page);

                    var startX = pageX * cellsPerPageX;
                    var startY = pageY * cellsPerPageY;
                    var chunkWidth = Math.Min(cellsPerPageX, grid.Width - startX);
                    var chunkHeight = Math.Min(cellsPerPageY, grid.Height - startY);

                    DrawGridChunk(gfx, grid, symbols, startX, startY, chunkWidth, chunkHeight);
                }
            }

            using var pdfStream = new MemoryStream();
            document.Save(pdfStream, false);
            return Task.FromResult<(byte[]?, string?)>((pdfStream.ToArray(), null));
        }
        catch (Exception ex)
        {
            return Task.FromResult<(byte[]?, string?)>((null, ex.ToString()));
        }
    }

    private static void AddLegendPage(PdfDocument document, Dictionary<System.Drawing.Color, int> colorSummary, Dictionary<System.Drawing.Color, string> symbols)
    {
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(8.5 * 72);
        page.Height = XUnit.FromPoint(11 * 72);
        
        XGraphics gfx = XGraphics.FromPdfPage(page);
        
        var titleFont = new XFont("Segoe UI", 14, XFontStyleEx.Bold);
        var headerFont = new XFont("Segoe UI", 9, XFontStyleEx.Bold);
        var bodyFont = new XFont("Segoe UI", 8, XFontStyleEx.Regular);
        var symbolFont = new XFont("Segoe UI", 7, XFontStyleEx.Bold);

        double yPos = 30;
        const double leftMargin = 30;
        const double pageBottomMargin = 30;
        const double rowHeight = 18;
        const double columnWidth = 190; // Narrower column width to fit 3 columns
        double currentColumnX = leftMargin;

        // 1. Calculate totals
        var totalColors = colorSummary.Count;
        var totalDiamonds = colorSummary.Values.Sum();

        // Draw Title and Summary Headers (stacked vertically)
        gfx.DrawString("Color Legend", titleFont, XBrushes.Black, new XRect(leftMargin, yPos, page.Width - (leftMargin * 2), 20), XStringFormats.TopLeft);
        yPos += 20;
        gfx.DrawString($"Total Colors: {totalColors}", bodyFont, XBrushes.Black, new XRect(leftMargin, yPos, page.Width - (leftMargin * 2), 15), XStringFormats.TopLeft);
        yPos += 15;
        gfx.DrawString($"Total Diamonds: {totalDiamonds}", bodyFont, XBrushes.Black, new XRect(leftMargin, yPos, page.Width - (leftMargin * 2), 15), XStringFormats.TopLeft);
        yPos += 25;
        
        var headerYPos = yPos; // Save the Y position for headers in new columns/pages

        // Draw Table Headers
        void DrawHeaders(XGraphics g, double x, double y)
        {
            g.DrawString("Order", headerFont, XBrushes.Black, x, y);
            g.DrawString("Color", headerFont, XBrushes.Black, x + 35, y);
            g.DrawString("DMC #", headerFont, XBrushes.Black, x + 70, y);
            g.DrawString("Count", headerFont, XBrushes.Black, x + 125, y);
        }

        DrawHeaders(gfx, currentColumnX, yPos);
        yPos += rowHeight;

        // 2. Sort data by count, descending
        var sortedSummary = colorSummary.OrderByDescending(kvp => kvp.Value).ToList();
        int order = 1;

        foreach (var entry in sortedSummary)
        {
            // 3. Handle column and page wrapping
            if (yPos > page.Height - pageBottomMargin)
            {
                currentColumnX += columnWidth; // Move to the next column
                yPos = headerYPos; // Reset Y to the top, below the main title

                // Check if the new column fits on the page
                if (currentColumnX + columnWidth > page.Width)
                {
                    gfx.Dispose();
                    page = document.AddPage();
                    page.Width = XUnit.FromPoint(8.5 * 72);
                    page.Height = XUnit.FromPoint(11 * 72);
                    gfx = XGraphics.FromPdfPage(page);
                    currentColumnX = leftMargin; // Reset to the first column
                    yPos = 30; // Reset Y fully for a new page
                    // Redraw main headers on new page if needed (optional)
                }
                
                DrawHeaders(gfx, currentColumnX, yPos);
                yPos += rowHeight;
            }

            var dmcColor = ColorMap.GetDMCColor(entry.Key);

            // Define column positions relative to the current column
            var orderColX = currentColumnX;
            var colorColX = orderColX + 35;
            var dmcColX = colorColX + 35;
            var countColX = dmcColX + 55;

            // Draw Order #
            gfx.DrawString(order.ToString(), bodyFont, XBrushes.Black, new XRect(orderColX, yPos, 30, rowHeight), XStringFormats.CenterLeft);

            // Draw Color circle w/ symbol
            var color = XColor.FromArgb(entry.Key.A, entry.Key.R, entry.Key.G, entry.Key.B);
            gfx.DrawEllipse(new XSolidBrush(color), colorColX, yPos, 15, 15);
            var brightness = (0.299 * entry.Key.R + 0.587 * entry.Key.G + 0.114 * entry.Key.B) / 255;
            var symbolBrush = brightness > 0.5 ? XBrushes.Black : XBrushes.White;
            gfx.DrawString(symbols[entry.Key], symbolFont, symbolBrush, new XRect(colorColX, yPos, 15, 15), XStringFormats.Center);

            // Draw DMC Number
            gfx.DrawString(dmcColor.DMCNumber.ToString(), bodyFont, XBrushes.Black, new XRect(dmcColX, yPos, 50, rowHeight), XStringFormats.CenterLeft);

            // Draw Count
            gfx.DrawString(entry.Value.ToString(), bodyFont, XBrushes.Black, new XRect(countColX, yPos, 50, rowHeight), XStringFormats.CenterLeft);

            yPos += rowHeight;
            order++;
        }
        gfx.Dispose(); // Dispose the final graphics context.
    }

    private static void DrawGridChunk(XGraphics gfx, DrillGrid grid, Dictionary<System.Drawing.Color, string> symbols, int startX, int startY, int width, int height)
    {
        var font = new XFont("Segoe UI", DrillWidthPoints * 0.6f, XFontStyleEx.Regular);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var gridY = startY + y;
                var gridX = startX + x;

                var cellColor = grid.Grid[gridY, gridX];
                if (cellColor.A > 0)
                {
                    var xColor = XColor.FromArgb(cellColor.A, cellColor.R, cellColor.G, cellColor.B);
                    var brush = new XSolidBrush(xColor);
                    var rect = new XRect(x * DrillWidthPoints, y * DrillWidthPoints, DrillWidthPoints, DrillWidthPoints);
                    gfx.DrawRectangle(brush, rect);

                    if (symbols.TryGetValue(cellColor, out var symbol))
                    {
                        var brightness = (0.299 * cellColor.R + 0.587 * cellColor.G + 0.114 * cellColor.B) / 255;
                        var symbolBrush = brightness > 0.5 ? XBrushes.Black : XBrushes.White;
                        gfx.DrawString(symbol, font, symbolBrush, rect, XStringFormats.Center);
                    }
                }
            }
        }
    }
}