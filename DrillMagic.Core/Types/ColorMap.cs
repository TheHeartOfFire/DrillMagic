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
        string? json = null;
        var asm = Assembly.GetExecutingAssembly();

        // Try to find embedded resource (recommended approach)
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("DefaultColorMap.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName is not null)
        {
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                using var reader = new System.IO.StreamReader(stream);
                json = reader.ReadToEnd();
            }
        }

        // Fallback: try the file on disk (keeps compatibility with Content+Copy deployments)
        if (string.IsNullOrWhiteSpace(json) && System.IO.File.Exists(_jsonFilePath))
        {
            json = System.IO.File.ReadAllText(_jsonFilePath);
        }

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("DefaultColorMap.json not found as embedded resource or content file. Ensure the JSON is deployed and accessible.");

        DefaultColorMap = System.Text.Json.JsonSerializer.Deserialize<Dictionary<uint, DMCColor>>(json) ?? [];

        // deserialize the custom mappings json file into the CustomMappings list
        var customDir = System.IO.Path.GetDirectoryName(_customMappingsFilePath) ?? string.Empty;
        System.IO.Directory.CreateDirectory(customDir);
        if (!System.IO.File.Exists(_customMappingsFilePath))
        {
            CustomMappings = new List<Dictionary<uint, string>>();
            return;
        }

        var customJson = System.IO.File.ReadAllText(_customMappingsFilePath);
        CustomMappings = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<uint, string>>>(customJson) ?? new List<Dictionary<uint, string>>();
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
