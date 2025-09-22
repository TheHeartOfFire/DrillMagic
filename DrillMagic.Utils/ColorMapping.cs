using DrillMagic.Core;
using DrillMagic.Core.Types;
using Microsoft.VisualBasic.FileIO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using Color = System.Drawing.Color;

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

    internal static void GenerateDMCImage(uint cellSize = 0)
    {
        var grid = new DrillGrid(_imageFilePath, 0, 0, cellSize);
        using var newImage = new Image<Rgba32>(grid.Width, grid.Height);

        for (int i = 0; i < grid.Height; i++)
        {
            for (int j = 0; j < grid.Width; j++)
            {
                var color = grid.Grid[i, j];
                if (color != Color.FromArgb(0, 0, 0, 0))
                    FillCellColor(newImage, j, i, cellSize, color);
            }
        }
        newImage.Save(_imageOutputFilePath);
    }

    private static void FillCellColor(Image<Rgba32> image, int x, int y, uint cellSize, Color color)
    {
        var cellX = (int)(x * cellSize);
        var cellY = (int)(y * cellSize);
        var rectangle = new Rectangle(cellX, cellY, (int)cellSize, (int)cellSize);
        var imageSharpColor = SixLabors.ImageSharp.Color.FromRgba(color.R, color.G, color.B, color.A);

        image.Mutate(ctx => ctx.Fill(imageSharpColor, rectangle));
    }
}
