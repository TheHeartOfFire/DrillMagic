namespace DrillMagic.Core.Export;

public class ImageExportOptions
{
    /// <summary>
    /// Rendered size in pixels for each grid cell/drill (default: 20 pixels).
    /// </summary>
    public int PixelsPerCell { get; set; } = 20;

    /// <summary>
    /// Whether to draw subtle grid cell border lines.
    /// </summary>
    public bool IncludeGridLines { get; set; } = true;
}

