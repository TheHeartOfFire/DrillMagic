using System;

namespace DrillMagic.Core.Projects;

public class PaletteEntry
{
    public int PaletteIndex { get; set; }
    public uint DmcNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Argb { get; set; } = "#00000000";
}

