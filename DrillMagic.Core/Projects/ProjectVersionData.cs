using System;
using System.Collections.Generic;

namespace DrillMagic.Core.Projects;

public class ProjectVersionData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public uint CellSize { get; set; }
    public float Rotation { get; set; }
    public float NormalizedStaggerOffset { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<PaletteEntry> Palette { get; set; } = [];
    /// <summary>
    /// Run-Length Encoded cell sequence: each array is [paletteIndex, count].
    /// </summary>
    public List<int[]> Cells { get; set; } = [];
}

