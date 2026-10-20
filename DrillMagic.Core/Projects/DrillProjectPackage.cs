using System.Collections.Generic;

namespace DrillMagic.Core.Projects;

public class DrillProjectPackage
{
    public ProjectManifest Manifest { get; set; } = new();
    public byte[]? SourceImageBytes { get; set; }
    public byte[]? MainThumbnail { get; set; }
    public Dictionary<string, ProjectVersionData> VersionsData { get; set; } = [];
    public Dictionary<string, byte[]> VersionThumbnails { get; set; } = [];
    public Dictionary<string, byte[]> SourceImageVersionsData { get; set; } = [];
}

