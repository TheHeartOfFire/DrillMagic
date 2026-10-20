using DrillMagic.Core.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DrillMagic.Core.Projects;

public class ZipProjectPackageService : IProjectPackageService
{
    private readonly ILogger<ZipProjectPackageService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ZipProjectPackageService(ILogger<ZipProjectPackageService>? logger = null)
    {
        _logger = logger ?? NullLogger<ZipProjectPackageService>.Instance;
    }

    public async Task<DrillProjectPackage> LoadAsync(Stream packageStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageStream);
        _logger.LogInformation("Loading DrillProjectPackage from stream");

        var package = new DrillProjectPackage();
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true);

        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidDataException("Invalid .drillmagic package: missing manifest.json.");

        await using (var manifestStream = manifestEntry.Open())
        {
            package.Manifest = await JsonSerializer.DeserializeAsync<ProjectManifest>(manifestStream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Failed to deserialize manifest.json.");
        }

        // Load source image if available
        if (!string.IsNullOrWhiteSpace(package.Manifest.SourceImageFileName))
        {
            var imageEntry = FindEntry(archive, package.Manifest.SourceImageFileName);
            if (imageEntry != null)
            {
                using var imgStream = imageEntry.Open();
                using var ms = new MemoryStream();
                await imgStream.CopyToAsync(ms, cancellationToken);
                package.SourceImageBytes = ms.ToArray();
            }
        }

        // Load base image versions history
        foreach (var imgVersion in package.Manifest.SourceImageHistory)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string imgFile = string.IsNullOrWhiteSpace(imgVersion.ImageFileName)
                ? $"assets/images/{imgVersion.Id}.png"
                : imgVersion.ImageFileName;

            var imgEntry = FindEntry(archive, imgFile);
            if (imgEntry != null)
            {
                using var iStream = imgEntry.Open();
                using var ms = new MemoryStream();
                await iStream.CopyToAsync(ms, cancellationToken);
                package.SourceImageVersionsData[imgVersion.Id] = ms.ToArray();
            }
        }

        // Backward compatibility: If no history exists but source image is present, create initial entry
        if (package.SourceImageBytes != null && package.SourceImageBytes.Length > 0 && package.Manifest.SourceImageHistory.Count == 0)
        {
            int w = 0, h = 0;
            try
            {
                var info = SixLabors.ImageSharp.Image.Identify(package.SourceImageBytes);
                if (info != null)
                {
                    w = info.Width;
                    h = info.Height;
                }
            }
            catch
            {
                // Ignore identification error if corrupted
            }

            string defaultId = "orig";
            var defaultSummary = new SourceImageVersionSummary
            {
                Id = defaultId,
                Name = "Original Image",
                CreatedUtc = package.Manifest.CreatedUtc,
                Width = w,
                Height = h,
                ByteSize = package.SourceImageBytes.Length,
                ImageFileName = package.Manifest.SourceImageFileName ?? "assets/source.png"
            };
            package.Manifest.SourceImageHistory.Add(defaultSummary);
            package.Manifest.ActiveSourceImageVersionId = defaultId;
            package.SourceImageVersionsData[defaultId] = package.SourceImageBytes;
        }

        // Load project thumbnail if present
        var thumbEntry = FindEntry(archive, "assets/thumbnail.png");
        if (thumbEntry != null)
        {
            using var tStream = thumbEntry.Open();
            using var ms = new MemoryStream();
            await tStream.CopyToAsync(ms, cancellationToken);
            package.MainThumbnail = ms.ToArray();
        }

        // Load versions
        foreach (var versionSummary in package.Manifest.Versions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string dataFile = string.IsNullOrWhiteSpace(versionSummary.DataFile)
                ? $"versions/{versionSummary.Id}.json"
                : versionSummary.DataFile;

            var versionEntry = FindEntry(archive, dataFile);
            if (versionEntry != null)
            {
                await using var vStream = versionEntry.Open();
                var versionData = await JsonSerializer.DeserializeAsync<ProjectVersionData>(vStream, JsonOptions, cancellationToken);
                if (versionData != null)
                {
                    package.VersionsData[versionSummary.Id] = versionData;
                }
            }

            if (!string.IsNullOrWhiteSpace(versionSummary.ThumbnailFile))
            {
                var vThumbEntry = FindEntry(archive, versionSummary.ThumbnailFile);
                if (vThumbEntry != null)
                {
                    using var vtStream = vThumbEntry.Open();
                    using var ms = new MemoryStream();
                    await vtStream.CopyToAsync(ms, cancellationToken);
                    package.VersionThumbnails[versionSummary.Id] = ms.ToArray();
                }
            }
        }

        _logger.LogInformation("Loaded package {ProjectId} with {VersionCount} versions",
            package.Manifest.ProjectId, package.VersionsData.Count);

        return package;
    }

    public async Task SaveAsync(DrillProjectPackage package, Stream targetStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(targetStream);
        _logger.LogInformation("Saving DrillProjectPackage {ProjectId} to stream", package.Manifest.ProjectId);

        package.Manifest.LastModifiedUtc = DateTimeOffset.UtcNow;

        // Set manifest image properties before serializing manifest
        if (package.SourceImageBytes != null && package.SourceImageBytes.Length > 0)
        {
            package.Manifest.HasSourceImage = true;
            if (string.IsNullOrWhiteSpace(package.Manifest.SourceImageFileName))
            {
                package.Manifest.SourceImageFileName = "assets/source.png";
            }
            package.Manifest.SourceImageFileName = package.Manifest.SourceImageFileName.Replace('\\', '/');
        }

        using var archive = new ZipArchive(targetStream, ZipArchiveMode.Create, leaveOpen: true);

        // 1. Write manifest.json
        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using (var mStream = manifestEntry.Open())
        {
            await JsonSerializer.SerializeAsync(mStream, package.Manifest, JsonOptions, cancellationToken);
        }

        // 2. Write source image if present
        var writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (package.SourceImageBytes != null && package.SourceImageBytes.Length > 0)
        {
            string imgFileName = (package.Manifest.SourceImageFileName ?? "assets/source.png").Replace('\\', '/').TrimStart('/');
            var imgEntry = archive.CreateEntry(imgFileName, CompressionLevel.Optimal);
            await using var iStream = imgEntry.Open();
            await iStream.WriteAsync(package.SourceImageBytes, cancellationToken);
            writtenEntries.Add(imgFileName);
        }

        // 2b. Write base image versions history files if present
        foreach (var kvp in package.SourceImageVersionsData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string imgId = kvp.Key;
            byte[] imgBytes = kvp.Value;
            if (imgBytes != null && imgBytes.Length > 0)
            {
                var summary = package.Manifest.SourceImageHistory.FirstOrDefault(s => s.Id == imgId);
                string entryPath = summary != null && !string.IsNullOrWhiteSpace(summary.ImageFileName)
                    ? summary.ImageFileName.Replace('\\', '/').TrimStart('/')
                    : $"assets/images/{imgId}.png";

                if (writtenEntries.Add(entryPath))
                {
                    var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                    await using var imgStream = entry.Open();
                    await imgStream.WriteAsync(imgBytes, cancellationToken);
                }
            }
        }

        // 3. Write main thumbnail if present
        if (package.MainThumbnail != null && package.MainThumbnail.Length > 0)
        {
            var thumbEntry = archive.CreateEntry("assets/thumbnail.png", CompressionLevel.Optimal);
            await using var tStream = thumbEntry.Open();
            await tStream.WriteAsync(package.MainThumbnail, cancellationToken);
        }

        // 4. Write version data files
        foreach (var kvp in package.VersionsData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string versionId = kvp.Key;
            var versionData = kvp.Value;

            string dataFile = $"versions/{versionId}.json";
            var vEntry = archive.CreateEntry(dataFile, CompressionLevel.Optimal);
            await using var vStream = vEntry.Open();
            await JsonSerializer.SerializeAsync(vStream, versionData, JsonOptions, cancellationToken);

            // Check if thumbnail exists for this version
            if (package.VersionThumbnails.TryGetValue(versionId, out var vThumb) && vThumb.Length > 0)
            {
                string thumbPath = $"assets/thumbnails/{versionId}.png";
                var vtEntry = archive.CreateEntry(thumbPath, CompressionLevel.Optimal);
                await using var vtStream = vtEntry.Open();
                await vtStream.WriteAsync(vThumb, cancellationToken);
            }
        }

        _logger.LogInformation("Saved package with {VersionCount} versions successfully", package.VersionsData.Count);
    }

    public ProjectVersionData CreateVersionData(string versionId, string versionName, DrillGrid grid, IColorMapService colorMapService)
    {
        ArgumentNullException.ThrowIfNull(versionId);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(colorMapService);

        var (palette, cells) = RleGridCodec.Encode(grid.Grid, colorMapService);

        return new ProjectVersionData
        {
            Id = versionId,
            Name = string.IsNullOrWhiteSpace(versionName) ? $"Version {versionId}" : versionName,
            CreatedUtc = DateTimeOffset.UtcNow,
            CellSize = grid.CellSize,
            Rotation = grid.Rotation,
            NormalizedStaggerOffset = grid.NormalizedStaggerOffset,
            Width = grid.Width,
            Height = grid.Height,
            Palette = palette,
            Cells = cells
        };
    }

    public DrillGrid RestoreGridFromVersion(ProjectVersionData versionData, IDrillGridFactory gridFactory)
    {
        ArgumentNullException.ThrowIfNull(versionData);
        ArgumentNullException.ThrowIfNull(gridFactory);

        var colors = RleGridCodec.Decode(versionData.Width, versionData.Height, versionData.Palette, versionData.Cells);
        return gridFactory.CreateGrid(colors, versionData.CellSize, versionData.Rotation, versionData.NormalizedStaggerOffset);
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = path.Replace('\\', '/').TrimStart('/');
        return archive.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName.Replace('\\', '/').TrimStart('/'), normalized, StringComparison.OrdinalIgnoreCase));
    }
}

