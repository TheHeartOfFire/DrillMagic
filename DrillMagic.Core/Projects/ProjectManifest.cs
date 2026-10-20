using System;
using System.Collections.Generic;

namespace DrillMagic.Core.Projects;

public class ProjectManifest
{
    public int FormatVersion { get; set; } = 1;
    public string ProjectId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Untitled Project";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastModifiedUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool HasSourceImage { get; set; }
    public string? SourceImageFileName { get; set; }
    public string ActiveVersionId { get; set; } = string.Empty;
    public List<ProjectVersionSummary> Versions { get; set; } = [];
    public string? ActiveSourceImageVersionId { get; set; }
    public List<SourceImageVersionSummary> SourceImageHistory { get; set; } = [];
}

