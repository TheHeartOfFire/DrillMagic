using System;

namespace DrillMagic.Core.Projects;

/// <summary>
/// Represents a historical snapshot of a source/base image iteration.
/// </summary>
public class SourceImageVersionSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Width { get; set; }
    public int Height { get; set; }
    public long ByteSize { get; set; }
    public string ImageFileName { get; set; } = string.Empty;

    public string SummaryText => $"{Width} × {Height} px • {FormatByteSize(ByteSize)}";

    private static string FormatByteSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

