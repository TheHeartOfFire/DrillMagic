using DrillMagic.Core.Types;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace DrillMagic.Core.Projects;

public static class RleGridCodec
{
    public static (List<PaletteEntry> Palette, List<int[]> Cells) Encode(Color[,] grid, IColorMapService colorMapService)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(colorMapService);

        int height = grid.GetLength(0);
        int width = grid.GetLength(1);

        if (height == 0 || width == 0)
        {
            return ([], []);
        }

        var palette = new List<PaletteEntry>();
        var colorToIndex = new Dictionary<Color, int>();
        var cells = new List<int[]>();

        int? currentPaletteIndex = null;
        int currentCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = grid[y, x];
                if (!colorToIndex.TryGetValue(color, out int pIndex))
                {
                    pIndex = palette.Count;
                    colorToIndex[color] = pIndex;

                    var dmc = colorMapService.GetDMCColor(color);
                    string hex = ColorTranslator.ToHtml(color);
                    palette.Add(new PaletteEntry
                    {
                        PaletteIndex = pIndex,
                        DmcNumber = dmc?.DMCNumber ?? uint.MaxValue,
                        Name = dmc?.Name ?? "Unknown",
                        Argb = hex
                    });
                }

                if (currentPaletteIndex == pIndex)
                {
                    currentCount++;
                }
                else
                {
                    if (currentPaletteIndex.HasValue)
                    {
                        cells.Add([currentPaletteIndex.Value, currentCount]);
                    }
                    currentPaletteIndex = pIndex;
                    currentCount = 1;
                }
            }
        }

        if (currentPaletteIndex.HasValue && currentCount > 0)
        {
            cells.Add([currentPaletteIndex.Value, currentCount]);
        }

        return (palette, cells);
    }

    public static Color[,] Decode(int width, int height, IReadOnlyList<PaletteEntry> palette, IReadOnlyList<int[]> cells)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(cells);

        if (width <= 0 || height <= 0)
        {
            return new Color[0, 0];
        }

        var grid = new Color[height, width];
        var paletteLookup = new Dictionary<int, Color>();

        foreach (var entry in palette)
        {
            try
            {
                paletteLookup[entry.PaletteIndex] = ColorTranslator.FromHtml(entry.Argb);
            }
            catch
            {
                paletteLookup[entry.PaletteIndex] = Color.Transparent;
            }
        }

        int totalExpectedCells = width * height;
        int flatIndex = 0;

        foreach (var run in cells)
        {
            if (run.Length < 2)
            {
                throw new InvalidDataException("Invalid RLE data: run entry must have [paletteIndex, count].");
            }

            int pIndex = run[0];
            int count = run[1];

            if (!paletteLookup.TryGetValue(pIndex, out var color))
            {
                color = Color.Transparent;
            }

            for (int i = 0; i < count; i++)
            {
                if (flatIndex >= totalExpectedCells)
                {
                    throw new InvalidDataException($"RLE cell count exceeds grid bounds ({totalExpectedCells}).");
                }

                int y = flatIndex / width;
                int x = flatIndex % width;
                grid[y, x] = color;
                flatIndex++;
            }
        }

        if (flatIndex != totalExpectedCells)
        {
            throw new InvalidDataException($"RLE decoded cell count ({flatIndex}) did not match expected grid cell count ({totalExpectedCells}).");
        }

        return grid;
    }
}

