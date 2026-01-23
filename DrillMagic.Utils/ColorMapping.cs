using DrillMagic.Core;
using DrillMagic.Core.Types;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DrillMagic.Utils;

public static class ColorMapping
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public static Dictionary<uint, DMCColor> ParseCsvToColorMap(string csvFilePath)
    {
        var result = new Dictionary<uint, DMCColor>();

        if (!File.Exists(csvFilePath))
        {
            throw new FileNotFoundException($"CSV file not found: {csvFilePath}");
        }

        using TextFieldParser parser = new(csvFilePath)
        {
            TextFieldType = FieldType.Delimited
        };
        parser.SetDelimiters(",");

        while (!parser.EndOfData)
        {
            string[]? fields = parser.ReadFields();
            if (fields == null) continue;

            // Assuming CSV format: ..., Number, ..., Name, ..., Hex, ...
            // Index 1: Number, Index 3: Name, Index 5: Hex
            if (fields.Length > 5 && uint.TryParse(fields[1], out uint dmcNumber))
            {
                result[dmcNumber] = new DMCColor(fields[3], dmcNumber, fields[5]);
            }
        }

        return result;
    }

    public static void PrintColorMap(IReadOnlyDictionary<uint, DMCColor> colorMap)
    {
        foreach (var color in colorMap.Values)
        {
            Console.WriteLine($"Name: {color.Name}; " +
                $"DMC#: {color.DMCNumber}; " +
                $"Hex: {color.Hex}; " +
                $"ARGB: {color.Color.A}, {color.Color.R}, {color.Color.G}, {color.Color.B}");
        }
    }

    public static void GenerateDefaultColorMapFile(string csvInputPath, string jsonOutputPath)
    {
        var map = ParseCsvToColorMap(csvInputPath);
        
        var dir = Path.GetDirectoryName(jsonOutputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(jsonOutputPath, JsonSerializer.Serialize(map, _jsonOptions));
    }
}
