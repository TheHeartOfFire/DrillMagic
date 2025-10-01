using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Environment;

namespace DrillMagic.Core.Types;
public class ColorMap
{
    public static Dictionary<uint, DMCColor> DefaultColorMap { get; private set; } = [];
    public static List<Dictionary<uint, string>> CustomMappings { get; private set; } = [];
    private static readonly string _rootPath = Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty)?.Parent?.Parent?.Parent?.Parent?.Parent?.Parent?.FullName ?? string.Empty;
    private static readonly string _jsonFilePath = Path.Combine(_rootPath, "DrillMagic.Core/Resources/DefaultColorMap.json");
    private static readonly string _customMappingsFilePath = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData),
        "Drill Magic/Color Mapping/CustomMappings.json");
    public Dictionary<uint, string> Mapping { get; private set; } = [];
    public string Name { get; set; } = string.Empty;

    public static void Initialize()
    {
        // deserialize the json file into the DefaultColorMap dictionary
        var json = File.ReadAllText(_jsonFilePath);
        DefaultColorMap = System.Text.Json.JsonSerializer.Deserialize<Dictionary<uint, DMCColor>>(json) ?? [];
        // deserialize the custom mappings json file into the CustomMappings list
        Directory.CreateDirectory(_customMappingsFilePath[.._customMappingsFilePath.LastIndexOf('/')]);
        if (!File.Exists(_customMappingsFilePath)) return;

        var customJson = File.ReadAllText(_customMappingsFilePath);
        CustomMappings = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<uint, string>>>(customJson) ?? [];
        

    }

    public static void SaveCustomMappings()
    {
        // serialize the CustomMappings list into a json file
        var json = System.Text.Json.JsonSerializer.Serialize(CustomMappings);
        File.WriteAllText(_customMappingsFilePath, json);
    }

    public static DMCColor GetDMCColor(uint dmcNumber)
    {
        if (DefaultColorMap.TryGetValue(dmcNumber, out DMCColor? value))
            return value;
        return DMCColor.Empty;
    }
    public static DMCColor GetDMCColor(Color color)
    {
        foreach (var dmcColor in DefaultColorMap.Values)
        {
            if (dmcColor.Color.ToArgb() == color.ToArgb())
                return dmcColor;
        }
        return DMCColor.Empty;
    }

    public ColorMap()
    {
        
    }



}
