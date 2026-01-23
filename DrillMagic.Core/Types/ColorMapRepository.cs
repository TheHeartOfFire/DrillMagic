using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ILogger<ColorMapRepository> _logger;

    public IReadOnlyDictionary<uint, DMCColor> DefaultColorMap { get; private set; } = new Dictionary<uint, DMCColor>();
    public List<Dictionary<uint, string>> CustomMappings { get; private set; } = [];

    private static readonly string _customMappingsFilePath = Path.Combine(GetFolderPath(SpecialFolder.ApplicationData),
        "Drill Magic", "Color Mapping", "CustomMappings.json");

    public ColorMapRepository(ILogger<ColorMapRepository>? logger = null)
    {
        _logger = logger ?? NullLogger<ColorMapRepository>.Instance;
        Initialize();
    }

    private void Initialize()
    {
        _logger.LogInformation("Initializing ColorMapRepository...");
        string? json = null;
        var asm = Assembly.GetExecutingAssembly();

        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("DefaultColorMap.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is not null)
        {
            _logger.LogDebug("Found embedded resource: {ResourceName}", resourceName);
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }
        }
        else
        {
            _logger.LogWarning("DefaultColorMap.json embedded resource not found.");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            var localPath = Path.Combine(AppContext.BaseDirectory, "DefaultColorMap.json");
            _logger.LogDebug("Checking local path: {LocalPath}", localPath);
            if (File.Exists(localPath))
            {
                json = File.ReadAllText(localPath);
            }
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            var ex = new InvalidOperationException("DefaultColorMap.json not found as embedded resource or in execution directory.");
            _logger.LogCritical(ex, "Failed to load default color map.");
            throw ex;
        }

        try 
        {
            DefaultColorMap = JsonSerializer.Deserialize<Dictionary<uint, DMCColor>>(json) ?? [];
            _logger.LogInformation("Loaded {Count} colors from default color map.", DefaultColorMap.Count);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to deserialize default color map JSON.");
            throw;
        }

        var customDir = Path.GetDirectoryName(_customMappingsFilePath);
        if (!string.IsNullOrEmpty(customDir))
        {
            Directory.CreateDirectory(customDir);
        }

        if (File.Exists(_customMappingsFilePath))
        {
            try
            {
                var customJson = File.ReadAllText(_customMappingsFilePath);
                CustomMappings = JsonSerializer.Deserialize<List<Dictionary<uint, string>>>(customJson) ?? [];
                _logger.LogInformation("Loaded {Count} custom mappings from {Path}", CustomMappings.Count, _customMappingsFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load custom mappings from {Path}", _customMappingsFilePath);
                CustomMappings = [];
            }
        }
        else
        {
            _logger.LogInformation("No custom mappings file found at {Path}", _customMappingsFilePath);
            CustomMappings = [];
        }
    }

    public void SaveCustomMappings()
    {
        try
        {
            _logger.LogInformation("Saving {Count} custom mappings to {Path}", CustomMappings.Count, _customMappingsFilePath);
            var json = JsonSerializer.Serialize(CustomMappings);
            var dir = Path.GetDirectoryName(_customMappingsFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_customMappingsFilePath, json);
            _logger.LogInformation("Custom mappings saved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save custom mappings.");
            throw;
        }
    }
}