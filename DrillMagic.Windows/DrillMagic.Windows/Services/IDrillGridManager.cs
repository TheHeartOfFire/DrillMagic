using DrillMagic.Core;
using DrillMagic.Core.Projects;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DrillMagic.Windows.Services;

public interface IDrillGridManager : INotifyPropertyChanged
{
    DrillGrid? SelectedGrid { get; }
    ObservableCollection<uint> AvailableCellSizes { get; }
    Task LoadImageAsync(MemoryStream imageStream, string? sourceImageName = null, CancellationToken cancellationToken = default);
    Task SelectGridAsync(uint cellSize);

    // Project & Versioning extensions
    DrillProjectPackage? CurrentProject { get; }
    string? CurrentFilePath { get; }
    string ProjectName { get; set; }
    bool IsDirty { get; set; }
    bool HasSavedProject { get; }
    bool HasSourceImage { get; }
    byte[]? SourceImageBytes { get; }
    string? SourceImageName { get; set; }
    DrillMagic.Core.Sizing.DrillShape ActiveDrillShape { get; set; }
    ObservableCollection<ProjectVersionSummary> Versions { get; }
    ProjectVersionSummary? ActiveVersion { get; }

    ObservableCollection<SourceImageVersionSummary> SourceImageHistory { get; }
    SourceImageVersionSummary? ActiveSourceImageVersion { get; }

    Task LoadProjectAsync(Stream stream, string filePath, CancellationToken cancellationToken = default);
    Task SaveProjectAsync(string filePath, CancellationToken cancellationToken = default);
    Task<ProjectVersionSummary> CreateVersionSnapshotAsync(string versionName, CancellationToken cancellationToken = default);
    Task SwitchToVersionAsync(string versionId, CancellationToken cancellationToken = default);
    Task<SourceImageVersionSummary> AddSourceImageVersionAsync(byte[] imageBytes, string versionName, CancellationToken cancellationToken = default);
    Task SwitchToSourceImageVersionAsync(string versionId, CancellationToken cancellationToken = default);
    Task ApplyDoctoredImageAsync(MemoryStream doctoredStream, string versionName = "Doctored Image", CancellationToken cancellationToken = default);
    Task<int> EnforceMinimumColorCountAsync(int minCount, CancellationToken cancellationToken = default);
    int GetColorsBelowThresholdCount(int minCount);
    int SourceImageWidth { get; }
    int SourceImageHeight { get; }
    DrillMagic.Core.Sizing.GridScalingAnalysis? AnalyzeTargetDimensions(int targetGridWidth, int targetGridHeight);
    Task SelectGridByDimensionsAsync(int targetGridWidth, int targetGridHeight, CancellationToken cancellationToken = default);
    void SetSelectedGrid(DrillGrid grid, bool markDirty = true);
    void MarkDirty(bool isDirty = true);
}

