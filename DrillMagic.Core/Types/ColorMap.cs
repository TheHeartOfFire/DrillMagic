using System.Collections.Generic;

namespace DrillMagic.Core.Types;

/// <summary>
/// Represents a specific color mapping configuration.
/// Data object only. Use IColorMapService for operations.
/// </summary>
public class ColorMap
{
    public Dictionary<uint, string> Mapping { get; private set; } = [];
    public string Name { get; set; } = string.Empty;
}