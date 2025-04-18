using DrillMagic.Core;
using DrillMagic.Core.Types;
using Microsoft.CodeAnalysis;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace DrillMagic.Utils;
internal static class ColorMapping
{
    private static readonly string _rootPath = Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty)?.Parent?.Parent?.Parent?.FullName ?? string.Empty;
    
    private static readonly string _imageFilePath = Path.Combine(_rootPath, "DrillMagic.Utils/Resources/AlbumArt1.jpeg"); 
    private static readonly string _imageOutputFilePath = Path.Combine(_rootPath, "DrillMagic.Utils/Resources/AlbumArt1(10) -HSL.png");

    private static readonly string _csvFilePath = Path.Combine(_rootPath, "DrillMagic.Utils/Resources/DMC Colors.csv");
    private static readonly string _outputFilePath = Path.Combine(_rootPath, "DrillMagic.Core/Resources/DefaultColorMap.json");

    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    internal static void PrintColorMap()
    {
        using TextFieldParser parser = new(_csvFilePath)
        {
            TextFieldType = FieldType.Delimited
        };
        parser.SetDelimiters(",");
        while (!parser.EndOfData)
        {
            string[]? fields = parser.ReadFields();
            if (fields == null) continue;

            if (uint.TryParse(fields[1], out uint dmcNumber))
                ColorMap.DefaultColorMap.Add(dmcNumber, new DMCColor(fields[3], dmcNumber, fields[5]));
        }
        for (int i = 0; i < ColorMap.DefaultColorMap.Count; i++)
        {
            var color = ColorMap.DefaultColorMap[ColorMap.DefaultColorMap.Keys.ElementAt(i)];
            Console.WriteLine($"Name: {color.Name}; " +
                $"DMC#: {color.DMCNumber}; " +
                $"Hex: {color.Hex}; " +
                $"ARGB: {color.Color.A}, {color.Color.R}, {color.Color.G}, {color.Color.B}");
        }
    }

    internal static void GenerateDefaultColorMap()
    {
        using TextFieldParser parser = new(_csvFilePath)
        {
            TextFieldType = FieldType.Delimited
        };
        parser.SetDelimiters(",");
        while (!parser.EndOfData)
        {
            string[]? fields = parser.ReadFields();
            if (fields == null) continue;

            if (uint.TryParse(fields[1], out uint dmcNumber))
                ColorMap.DefaultColorMap.Add(dmcNumber, new DMCColor(fields[3], dmcNumber, fields[5]));

        }
        File.WriteAllText(_outputFilePath, System.Text.Json.JsonSerializer.Serialize(ColorMap.DefaultColorMap, _jsonOptions));
    }

    [SupportedOSPlatform("windows6.1")]
    internal static void GenerateDMCImage(uint cellSize = 0)
    {
        var image = new Bitmap(_imageFilePath);
        ColorMap.Initialize();

        var grid = new DrillGrid(image,0,0,cellSize);
        var newImage = new Bitmap(grid.Width, grid.Height);
        for (int i = 0; i < grid.Height; i++)
        {
            for (int j = 0; j < grid.Width; j++)
            {
                var color = grid.Grid[i, j];
                if (color != Color.FromArgb(0,0,0,0))
                    FillCellColor(newImage, j, i, cellSize, color);
            }
        }
        newImage.Save(_imageOutputFilePath, System.Drawing.Imaging.ImageFormat.Png);
    }

    [SupportedOSPlatform("windows6.1")]
    private static void FillCellColor(Bitmap image, int x, int y, uint cellSize, Color color)
    {
        x = (int)(x * cellSize);
        y = (int)(y * cellSize);
        for (int i = 0; i < cellSize; i++)
        {
            for (int j = 0; j < cellSize; j++)
            {
                if (x + i < image.Width && y + j < image.Height)
                    image.SetPixel(x + i, y + j, color);
            }
        }
    }
}
