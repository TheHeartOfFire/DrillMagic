using CommunityToolkit.Mvvm.ComponentModel;
using DrillMagic.Core;
using DrillMagic.Core.Projects;
using DrillMagic.Core.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DrillMagic.Windows.Services;

public partial class DrillGridManager(
    IColorMapService colorMapService,
    IServiceProvider serviceProvider,
    ILogger<DrillGridManager> logger) : ObservableObject, IDrillGridManager
{
    private readonly ILogger<DrillGridManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IColorMapService _colorMapService = colorMapService ?? throw new ArgumentNullException(nameof(colorMapService));

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private DrillGrid? _selectedGrid;

    [ObservableProperty]
    private ObservableCollection<uint> _availableCellSizes = [];

    [ObservableProperty]
    private string? _currentFilePath;

    [ObservableProperty]
    private string _projectName = "Untitled Project";

    [ObservableProperty]
    private string? _sourceImageName;

    [ObservableProperty]
    private bool _isDirty;

    public bool HasSavedProject => !string.IsNullOrWhiteSpace(CurrentFilePath);

    partial void OnCurrentFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSavedProject));
    }

    [ObservableProperty]
    private ObservableCollection<ProjectVersionSummary> _versions = [];

    [ObservableProperty]
    private ProjectVersionSummary? _activeVersion;

    [ObservableProperty]
    private ObservableCollection<SourceImageVersionSummary> _sourceImageHistory = [];

    [ObservableProperty]
    private SourceImageVersionSummary? _activeSourceImageVersion;

    [ObservableProperty]
    private DrillMagic.Core.Sizing.DrillShape _activeDrillShape = DrillMagic.Core.Sizing.DrillShape.Square;

    [ObservableProperty]
    private int _sourceImageWidth;

    [ObservableProperty]
    private int _sourceImageHeight;

    public bool HasSourceImage => _imageStream != null || _currentProject?.SourceImageBytes != null;

    public byte[]? SourceImageBytes => _currentProject?.SourceImageBytes ?? _imageStream?.ToArray();

    private DrillProjectPackage? _currentProject;
    public DrillProjectPackage? CurrentProject => _currentProject;

    private readonly Dictionary<uint, DrillGrid> _grids = [];
    private MemoryStream? _imageStream;

    private IProjectPackageService PackageService =>
        _serviceProvider.GetService<IProjectPackageService>() ?? new ZipProjectPackageService();

    private IDrillGridFactory GridFactory =>
        _serviceProvider.GetService<IDrillGridFactory>() ?? new DrillGridFactory(_colorMapService);

    public async Task LoadImageAsync(MemoryStream imageStream, string? sourceImageName = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting LoadImageAsync");
        IsBusy = true;
        try
        {
            _imageStream = imageStream;
            _grids.Clear();
            AvailableCellSizes.Clear();
            Versions.Clear();
            ActiveVersion = null;
            SourceImageHistory.Clear();
            ActiveSourceImageVersion = null;
            CurrentFilePath = null;

            SourceImageName = !string.IsNullOrWhiteSpace(sourceImageName) ? sourceImageName : null;
            ProjectName = !string.IsNullOrWhiteSpace(sourceImageName)
                ? Path.GetFileNameWithoutExtension(sourceImageName)
                : "Untitled Project";

            byte[] initialImageBytes = imageStream.ToArray();

            // Initialize new project package container
            _currentProject = new DrillProjectPackage
            {
                Manifest = new ProjectManifest
                {
                    Name = ProjectName,
                    HasSourceImage = true,
                    SourceImageFileName = "assets/source.png"
                },
                SourceImageBytes = initialImageBytes
            };

            int imgW = 0, imgH = 0;
            List<uint> sizes;
            using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imageStream, cancellationToken))
            {
                imgW = image.Width;
                imgH = image.Height;
                SourceImageWidth = imgW;
                SourceImageHeight = imgH;
                _logger.LogInformation("Image loaded. Dimensions: {Width}x{Height}", image.Width, image.Height);
                sizes = CalculateAvailableCellSizes(imgW, imgH);
            }

            // Create initial source image version entry
            string initialImgId = $"img_orig_{Guid.NewGuid():N}";
            var initialImgSummary = new SourceImageVersionSummary
            {
                Id = initialImgId,
                Name = !string.IsNullOrWhiteSpace(sourceImageName) ? Path.GetFileName(sourceImageName) : "Original Image",
                CreatedUtc = DateTimeOffset.UtcNow,
                Width = imgW,
                Height = imgH,
                ByteSize = initialImageBytes.Length,
                ImageFileName = $"assets/images/{initialImgId}.png"
            };
            _currentProject.Manifest.SourceImageHistory.Add(initialImgSummary);
            _currentProject.Manifest.ActiveSourceImageVersionId = initialImgId;
            _currentProject.SourceImageVersionsData[initialImgId] = initialImageBytes;
            SourceImageHistory.Add(initialImgSummary);
            ActiveSourceImageVersion = initialImgSummary;

            foreach (var size in sizes)
            {
                AvailableCellSizes.Add(size);
            }

            if (AvailableCellSizes.Count > 0)
            {
                var defaultIndex = Math.Min(9, AvailableCellSizes.Count - 1);
                var sizeToSelect = AvailableCellSizes[defaultIndex];
                _logger.LogInformation("Selecting default grid size: {Size} (Index: {Index})", sizeToSelect, defaultIndex);
                await SelectGridAsync(sizeToSelect);

                // Create initial snapshot version
                if (SelectedGrid != null)
                {
                    await CreateVersionSnapshotAsync("Initial Slicing", cancellationToken);
                }
            }
            else
            {
                _logger.LogWarning("No suitable cell sizes found.");
                SelectedGrid = null;
            }

            IsDirty = false;
            OnPropertyChanged(nameof(HasSourceImage));
            OnPropertyChanged(nameof(SourceImageBytes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LoadImageAsync");
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectGridAsync(uint cellSize)
    {
        _logger.LogInformation("SelectGridAsync called for size: {CellSize}", cellSize);
        if (_grids.TryGetValue(cellSize, out var grid))
        {
            SelectedGrid = grid;
            return;
        }

        if (_imageStream is not null)
        {
            _logger.LogInformation("Generating new DrillGrid for size {CellSize}", cellSize);
            try
            {
                var gridLogger = _serviceProvider.GetService<ILogger<DrillGrid>>();
                var newGrid = await Task.Run(() => new DrillGrid(_colorMapService, _imageStream, cellSize, 0, 0, gridLogger));
                _grids[cellSize] = newGrid;
                SelectedGrid = newGrid;
                _logger.LogInformation("DrillGrid generated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate DrillGrid for size {CellSize}", cellSize);
                throw;
            }
        }
        else
        {
            _logger.LogWarning("SelectGridAsync called but _imageStream is null");
        }
    }

    public async Task LoadProjectAsync(Stream stream, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _logger.LogInformation("Loading project from {FilePath}", filePath);
        IsBusy = true;
        try
        {
            var package = await PackageService.LoadAsync(stream, cancellationToken);
            _currentProject = package;
            CurrentFilePath = filePath;

            var nameFromFile = Path.GetFileNameWithoutExtension(filePath);
            ProjectName = !string.IsNullOrWhiteSpace(nameFromFile)
                ? nameFromFile
                : (!string.IsNullOrWhiteSpace(package.Manifest.Name) ? package.Manifest.Name : "Untitled Project");
            package.Manifest.Name = ProjectName;

            if (!string.IsNullOrWhiteSpace(package.Manifest.SourceImageFileName))
            {
                SourceImageName = Path.GetFileName(package.Manifest.SourceImageFileName);
            }
            else if (package.Manifest.SourceImageHistory.Count > 0)
            {
                SourceImageName = package.Manifest.SourceImageHistory[0].Name;
            }
            else
            {
                SourceImageName = null;
            }

            _grids.Clear();
            AvailableCellSizes.Clear();
            Versions.Clear();

            // Populate versions
            foreach (var vSummary in package.Manifest.Versions)
            {
                Versions.Add(vSummary);
            }

            // Restore source image if present
            if (package.SourceImageBytes != null && package.SourceImageBytes.Length > 0)
            {
                _imageStream = new MemoryStream(package.SourceImageBytes);
                using var image = SixLabors.ImageSharp.Image.Load(package.SourceImageBytes);
                SourceImageWidth = image.Width;
                SourceImageHeight = image.Height;
                foreach (var size in CalculateAvailableCellSizes(image.Width, image.Height))
                {
                    AvailableCellSizes.Add(size);
                }
            }
            else
            {
                _imageStream = null;
                SourceImageWidth = 0;
                SourceImageHeight = 0;
            }

            // Determine active version
            string activeId = package.Manifest.ActiveVersionId;
            if (string.IsNullOrWhiteSpace(activeId) && package.Manifest.Versions.Count > 0)
            {
                activeId = package.Manifest.Versions[0].Id;
            }

            if (!string.IsNullOrWhiteSpace(activeId) && package.VersionsData.TryGetValue(activeId, out var activeData))
            {
                var restoredGrid = PackageService.RestoreGridFromVersion(activeData, GridFactory);
                SelectedGrid = restoredGrid;
                ActiveVersion = Versions.FirstOrDefault(v => v.Id == activeId);
                if (restoredGrid.CellSize > 0)
                {
                    _grids[restoredGrid.CellSize] = restoredGrid;
                }
            }
            else
            {
                SelectedGrid = null;
                ActiveVersion = null;
            }

            IsDirty = false;
            OnPropertyChanged(nameof(HasSourceImage));
            OnPropertyChanged(nameof(SourceImageBytes));

            // Populate source image history
            SourceImageHistory.Clear();
            foreach (var imgSummary in package.Manifest.SourceImageHistory)
            {
                SourceImageHistory.Add(imgSummary);
            }
            if (!string.IsNullOrWhiteSpace(package.Manifest.ActiveSourceImageVersionId))
            {
                ActiveSourceImageVersion = SourceImageHistory.FirstOrDefault(s => s.Id == package.Manifest.ActiveSourceImageVersionId);
            }
            else if (SourceImageHistory.Count > 0)
            {
                ActiveSourceImageVersion = SourceImageHistory[0];
            }

            _logger.LogInformation("Project loaded successfully. Active version: {ActiveVersion}", ActiveVersion?.Name);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveProjectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _logger.LogInformation("Saving project to {FilePath}", filePath);
        IsBusy = true;
        try
        {
            CurrentFilePath = filePath;
            var nameFromFile = Path.GetFileNameWithoutExtension(filePath);
            if (!string.IsNullOrWhiteSpace(nameFromFile))
            {
                ProjectName = nameFromFile;
            }

            _currentProject ??= new DrillProjectPackage();
            _currentProject.Manifest.Name = ProjectName;

            // If a grid is currently active and versions are empty, create default version
            if (SelectedGrid != null && _currentProject.Manifest.Versions.Count == 0)
            {
                await CreateVersionSnapshotAsync("Initial Version", cancellationToken);
            }
            else if (SelectedGrid != null && ActiveVersion != null)
            {
                // Update current active version with any recent edits
                var updatedData = PackageService.CreateVersionData(ActiveVersion.Id, ActiveVersion.Name, SelectedGrid, _colorMapService);
                _currentProject.VersionsData[ActiveVersion.Id] = updatedData;

                ActiveVersion.CellSize = SelectedGrid.CellSize;
                ActiveVersion.Width = SelectedGrid.Width;
                ActiveVersion.Height = SelectedGrid.Height;
                ActiveVersion.ColorCount = SelectedGrid.ColorSummary.Count;
            }

            if (_imageStream != null && _currentProject.SourceImageBytes == null)
            {
                _currentProject.SourceImageBytes = _imageStream.ToArray();
                _currentProject.Manifest.HasSourceImage = true;
            }

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await PackageService.SaveAsync(_currentProject, fs, cancellationToken);

            CurrentFilePath = filePath;
            IsDirty = false;
            _logger.LogInformation("Project saved successfully to {FilePath}", filePath);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task<ProjectVersionSummary> CreateVersionSnapshotAsync(string versionName, CancellationToken cancellationToken = default)
    {
        if (SelectedGrid == null)
        {
            throw new InvalidOperationException("Cannot create version snapshot without a selected grid.");
        }

        _currentProject ??= new DrillProjectPackage();

        string versionId = $"v{Versions.Count + 1}_{Guid.NewGuid():N}";
        var versionData = PackageService.CreateVersionData(versionId, versionName, SelectedGrid, _colorMapService);
        _currentProject.VersionsData[versionId] = versionData;

        var summary = new ProjectVersionSummary
        {
            Id = versionId,
            Name = string.IsNullOrWhiteSpace(versionName) ? $"Version {Versions.Count + 1}" : versionName,
            CreatedUtc = DateTimeOffset.UtcNow,
            CellSize = SelectedGrid.CellSize,
            Width = SelectedGrid.Width,
            Height = SelectedGrid.Height,
            ColorCount = SelectedGrid.ColorSummary.Count,
            DataFile = $"versions/{versionId}.json"
        };

        _currentProject.Manifest.Versions.Add(summary);
        _currentProject.Manifest.ActiveVersionId = versionId;

        Versions.Add(summary);
        ActiveVersion = summary;
        IsDirty = true;

        _logger.LogInformation("Created snapshot version '{VersionName}' ({VersionId})", summary.Name, versionId);
        return Task.FromResult(summary);
    }

    public Task SwitchToVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        if (_currentProject == null || !_currentProject.VersionsData.TryGetValue(versionId, out var versionData))
        {
            throw new InvalidOperationException($"Version '{versionId}' not found in current project.");
        }

        _logger.LogInformation("Switching to version {VersionId}", versionId);
        var restoredGrid = PackageService.RestoreGridFromVersion(versionData, GridFactory);
        SelectedGrid = restoredGrid;

        _currentProject.Manifest.ActiveVersionId = versionId;
        ActiveVersion = Versions.FirstOrDefault(v => v.Id == versionId);
        IsDirty = false;

        return Task.CompletedTask;
    }

    public Task<SourceImageVersionSummary> AddSourceImageVersionAsync(byte[] imageBytes, string versionName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        _currentProject ??= new DrillProjectPackage();

        int w = 0, h = 0;
        try
        {
            var info = SixLabors.ImageSharp.Image.Identify(imageBytes);
            if (info != null)
            {
                w = info.Width;
                h = info.Height;
            }
        }
        catch
        {
            // Ignore identification error
        }

        string versionId = $"img_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
        var summary = new SourceImageVersionSummary
        {
            Id = versionId,
            Name = string.IsNullOrWhiteSpace(versionName) ? $"Image Iteration {SourceImageHistory.Count + 1}" : versionName,
            CreatedUtc = DateTimeOffset.UtcNow,
            Width = w,
            Height = h,
            ByteSize = imageBytes.Length,
            ImageFileName = $"assets/images/{versionId}.png"
        };

        _currentProject.Manifest.SourceImageHistory.Add(summary);
        _currentProject.SourceImageVersionsData[versionId] = imageBytes;
        SourceImageHistory.Add(summary);
        IsDirty = true;
        return Task.FromResult(summary);
    }

    public async Task SwitchToSourceImageVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        if (_currentProject == null || !_currentProject.SourceImageVersionsData.TryGetValue(versionId, out var imgBytes))
        {
            throw new InvalidOperationException($"Source image version '{versionId}' not found.");
        }

        _logger.LogInformation("Switching to source image version {VersionId}", versionId);
        _currentProject.SourceImageBytes = imgBytes;
        _currentProject.Manifest.ActiveSourceImageVersionId = versionId;
        _imageStream = new MemoryStream(imgBytes);

        int imgW = 0, imgH = 0;
        using (var image = await SixLabors.ImageSharp.Image.LoadAsync(_imageStream, cancellationToken))
        {
            imgW = image.Width;
            imgH = image.Height;
            SourceImageWidth = imgW;
            SourceImageHeight = imgH;
        }
        _imageStream.Position = 0;

        _grids.Clear();
        AvailableCellSizes.Clear();
        foreach (var size in CalculateAvailableCellSizes(imgW, imgH))
        {
            AvailableCellSizes.Add(size);
        }

        ActiveSourceImageVersion = SourceImageHistory.FirstOrDefault(s => s.Id == versionId);

        uint currentCellSize = SelectedGrid?.CellSize ?? 10;
        if (!AvailableCellSizes.Contains(currentCellSize))
        {
            currentCellSize = AvailableCellSizes.LastOrDefault();
            if (currentCellSize == 0) currentCellSize = 1;
        }

        await SelectGridAsync(currentCellSize);
        if (SelectedGrid != null)
        {
            await CreateVersionSnapshotAsync($"Re-sliced: {ActiveSourceImageVersion?.Name ?? versionId}", cancellationToken);
        }

        IsDirty = true;
        OnPropertyChanged(nameof(HasSourceImage));
        OnPropertyChanged(nameof(SourceImageBytes));
    }

    public async Task ApplyDoctoredImageAsync(MemoryStream doctoredStream, string versionName = "Doctored Image", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(doctoredStream);
        _logger.LogInformation("Applying doctored image");
        IsBusy = true;
        try
        {
            byte[] doctoredBytes = doctoredStream.ToArray();
            _currentProject ??= new DrillProjectPackage();
            _currentProject.SourceImageBytes = doctoredBytes;
            _imageStream = new MemoryStream(doctoredBytes);

            int imgW = 0, imgH = 0;
            using (var image = await SixLabors.ImageSharp.Image.LoadAsync(_imageStream, cancellationToken))
            {
                imgW = image.Width;
                imgH = image.Height;
                SourceImageWidth = imgW;
                SourceImageHeight = imgH;
            }
            _imageStream.Position = 0;

            string versionId = $"img_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
            var summary = new SourceImageVersionSummary
            {
                Id = versionId,
                Name = string.IsNullOrWhiteSpace(versionName) ? $"Doctored Image {SourceImageHistory.Count + 1}" : versionName,
                CreatedUtc = DateTimeOffset.UtcNow,
                Width = imgW,
                Height = imgH,
                ByteSize = doctoredBytes.Length,
                ImageFileName = $"assets/images/{versionId}.png"
            };

            _currentProject.Manifest.SourceImageHistory.Add(summary);
            _currentProject.Manifest.ActiveSourceImageVersionId = versionId;
            _currentProject.SourceImageVersionsData[versionId] = doctoredBytes;
            SourceImageHistory.Add(summary);
            ActiveSourceImageVersion = summary;

            _grids.Clear();
            AvailableCellSizes.Clear();
            foreach (var size in CalculateAvailableCellSizes(imgW, imgH))
            {
                AvailableCellSizes.Add(size);
            }

            uint currentCellSize = SelectedGrid?.CellSize ?? 10;
            if (!AvailableCellSizes.Contains(currentCellSize))
            {
                currentCellSize = AvailableCellSizes.LastOrDefault();
                if (currentCellSize == 0) currentCellSize = 1;
            }

            await SelectGridAsync(currentCellSize);
            if (SelectedGrid != null)
            {
                await CreateVersionSnapshotAsync($"Slicing: {summary.Name}", cancellationToken);
            }

            IsDirty = true;
            OnPropertyChanged(nameof(HasSourceImage));
            OnPropertyChanged(nameof(SourceImageBytes));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static List<uint> CalculateAvailableCellSizes(int width, int height)
    {
        var minDimension = Math.Min(width, height);
        var sizes = new List<uint>();
        uint maxSize = (uint)Math.Min(150, minDimension);
        for (uint size = 1; size <= maxSize; size++)
        {
            sizes.Add(size);
        }
        return sizes;
    }

    public int GetColorsBelowThresholdCount(int minCount)
    {
        return SelectedGrid?.GetColorsBelowThresholdCount(minCount) ?? 0;
    }

    public async Task<int> EnforceMinimumColorCountAsync(int minCount, CancellationToken cancellationToken = default)
    {
        if (SelectedGrid == null || minCount <= 1) return 0;

        _logger.LogInformation("Enforcing minimum drill count threshold: {MinCount}", minCount);
        int eliminated = SelectedGrid.EnforceMinimumColorCount(minCount);
        if (eliminated > 0)
        {
            IsDirty = true;
            await CreateVersionSnapshotAsync($"Min Count Threshold: {minCount} drills", cancellationToken);
        }
        return eliminated;
    }

    public DrillMagic.Core.Sizing.GridScalingAnalysis? AnalyzeTargetDimensions(int targetGridWidth, int targetGridHeight)
    {
        if (SourceImageWidth <= 0 || SourceImageHeight <= 0)
        {
            return null;
        }
        return DrillMagic.Core.Sizing.GridScalingAnalysis.Analyze(SourceImageWidth, SourceImageHeight, targetGridWidth, targetGridHeight);
    }

    public async Task SelectGridByDimensionsAsync(int targetGridWidth, int targetGridHeight, CancellationToken cancellationToken = default)
    {
        if (_imageStream == null)
        {
            throw new InvalidOperationException("No image loaded to generate grid from.");
        }

        var analysis = AnalyzeTargetDimensions(targetGridWidth, targetGridHeight);
        if (analysis == null || analysis.IsExceedingResolution || analysis.EffectiveCellSize == 0)
        {
            throw new InvalidOperationException($"Target grid dimensions ({targetGridWidth}×{targetGridHeight}) exceed source image resolution ({SourceImageWidth}×{SourceImageHeight} px).");
        }

        _logger.LogInformation("Creating grid with target dimensions {W}x{H} (effective cell size: {CellSize}, startOffset: {OffsetX},{OffsetY})",
            targetGridWidth, targetGridHeight, analysis.EffectiveCellSize, analysis.StartOffsetX, analysis.StartOffsetY);

        IsBusy = true;
        try
        {
            _imageStream.Position = 0;
            var grid = GridFactory.CreateGridFromImage(_imageStream, targetGridWidth, targetGridHeight, analysis.EffectiveCellSize);
            _grids[analysis.EffectiveCellSize] = grid;
            SelectedGrid = grid;
            IsDirty = true;

            await CreateVersionSnapshotAsync($"Dimensions: {targetGridWidth}×{targetGridHeight} drills", cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetSelectedGrid(DrillGrid grid, bool markDirty = true)
    {
        SelectedGrid = grid;
        if (markDirty)
        {
            IsDirty = true;
        }
    }

    public void MarkDirty(bool isDirty = true)
    {
        IsDirty = isDirty;
    }
}

