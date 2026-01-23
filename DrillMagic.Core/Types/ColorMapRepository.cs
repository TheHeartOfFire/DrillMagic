using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using static System.Environment;

namespace DrillMagic.Core.Types;

public class ColorMapRepository : IColorMapRepository
{
    public IReadOnlyDictionary<uint, DMCColor> DefaultColorMap { get; private set; } = new Dictionary<uint, DMCColor>();
    public List<Dictionary<uint, string>> CustomMappings { get; private set; } = [];

    private static readonly string _customMappingsFilePath = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData),
        "Drill Magic", "Color Mapping", "CustomMappings.json");

    public ColorMapRepository()
    {
        Initialize();
    }

    private void Initialize()
    {
        string? json = null;
        var asm = Assembly.GetExecutingAssembly();

        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("DefaultColorMap.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is not null)
        {
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            var localPath = Path.Combine(AppContext.BaseDirectory, "DefaultColorMap.json");
            if (File.Exists(localPath))
            {
                json = File.ReadAllText(localPath);
            }
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("DefaultColorMap.json not found as embedded resource or in execution directory.");
        }

        DefaultColorMap = JsonSerializer.Deserialize<Dictionary<uint, DMCColor>>(json) ?? [];

        var customDir = Path.GetDirectoryName(_customMappingsFilePath);
        if (!string.IsNullOrEmpty(customDir))
        {
            Directory.CreateDirectory(customDir);
        }

        if (File.Exists(_customMappingsFilePath))
        {
            var customJson = File.ReadAllText(_customMappingsFilePath);
            CustomMappings = JsonSerializer.Deserialize<List<Dictionary<uint, string>>>(customJson) ?? [];
        }
        else
        {
            CustomMappings = [];
        }
    }

    public void SaveCustomMappings()
    {
        var json = JsonSerializer.Serialize(CustomMappings);
        var dir = Path.GetDirectoryName(_customMappingsFilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_customMappingsFilePath, json);
    }
}