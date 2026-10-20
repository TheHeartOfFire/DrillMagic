using System;

namespace DrillMagic.Core.Projects;

public class ProjectVersionSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public uint CellSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int ColorCount { get; set; }
    public string DataFile { get; set; } = string.Empty;
    public string? ThumbnailFile { get; set; }
    public string SummaryText => $"{Width}x{Height} cells • {CellSize}px • {ColorCount} colors";
}

