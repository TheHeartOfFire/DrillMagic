using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace DrillMagic.Test.Services;

public class DrillGridManagerTests
{
    private readonly Mock<IColorMapService> _colorMapServiceMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<DrillGridManager>> _loggerMock;
    private readonly DrillGridManager _sut;

    public DrillGridManagerTests()
    {
        _colorMapServiceMock = new Mock<IColorMapService>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<DrillGridManager>>();
        
        // Setup service provider for logging used in DrillGrid constructor
        _serviceProviderMock
            .Setup(x => x.GetService(typeof(ILogger<DrillGrid>)))
            .Returns(new Mock<ILogger<DrillGrid>>().Object);
            
        // Setup default mock response for ColorMapService to avoid nulls
        _colorMapServiceMock
            .Setup(x => x.FindClosestDMCColor(It.IsAny<System.Drawing.Color>()))
            .Returns(new DMCColor("Test", 1, "#000000"));

        _sut = new DrillGridManager(
            _colorMapServiceMock.Object,
            _serviceProviderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeDefaults()
    {
        _sut.IsBusy.Should().BeFalse();
        _sut.SelectedGrid.Should().BeNull();
        _sut.AvailableCellSizes.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadImageAsync_ShouldPopulateCellSizes_AndSelectDefault()
    {
        // Arrange
        // Create a 100x100 image which allows cell sizes like 10, 20 etc.
        using var stream = CreateTestImageStream(100, 100);
        
        // Act
        await _sut.LoadImageAsync(stream);

        // Assert
        _sut.AvailableCellSizes.Should().NotBeEmpty();
        _sut.AvailableCellSizes.Should().Contain(10); // Since 10 < 100 and it's within range 1-20
        _sut.SelectedGrid.Should().NotBeNull();
        _sut.IsBusy.Should().BeFalse();
    }
    
    [Fact]
    public async Task LoadImageAsync_WithSmallImage_ShouldHaveLimitedCellSizes()
    {
         // Arrange
         // Image small enough to limit the loop (min dimension 5)
        using var stream = CreateTestImageStream(5, 5);
        
        // Act
        await _sut.LoadImageAsync(stream);

        // Assert
        // Logic: for (uint size = 1; size <= 20; size += 1) { if (size > minDimension) break; }
        // minDimension is 5.
        // size=1: add
        // size=2: add
        // size=3: add
        // size=4: add
        // size=5: add
        // size=6: break
        _sut.AvailableCellSizes.Should().HaveCount(5); 
        _sut.AvailableCellSizes.Should().ContainInOrder([1, 2, 3, 4, 5]);
        _sut.SelectedGrid.Should().NotBeNull();
    }
    
    [Fact]
    public async Task LoadImageAsync_ShouldSetSelectedGrid()
    {
        // Arrange
        using var stream = CreateTestImageStream(20, 20);

        // Act
        await _sut.LoadImageAsync(stream);

        // Assert
        _sut.SelectedGrid.Should().NotBeNull();
        _sut.SelectedGrid!.Width.Should().BeGreaterThan(0);
        _sut.SelectedGrid!.Height.Should().BeGreaterThan(0);
    }

    private static MemoryStream CreateTestImageStream(int width, int height)
    {
        var runStream = new MemoryStream();
        using var image = new Image<Rgba32>(width, height);
        // Fill with some color to ensure valid pixel data
        image.ProcessPixelRows(accessor => {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgba32(255, 0, 0, 255);
                }
            }
        });
        
        image.SaveAsPng(runStream);
        runStream.Position = 0;
        return runStream;
    }

    [Fact]
    public async Task LoadImageAsync_ShouldHandleException_AndResetState()
    {
        // Arrange
        var invalidStream = new MemoryStream([0, 0, 0, 0]); // Not a valid image
        
        // Act
        Func<Task> act = async () => await _sut.LoadImageAsync(invalidStream);

        // Assert
        await act.Should().ThrowAsync<Exception>();
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task SelectGridAsync_ShouldReuseCachedGrid_WhenCalledTwice()
    {
        // Arrange
        using var stream = CreateTestImageStream(10, 10);
        await _sut.LoadImageAsync(stream);
        var size = _sut.AvailableCellSizes[0];

        // Act
        await _sut.SelectGridAsync(size);
        var firstGrid = _sut.SelectedGrid;
        
        await _sut.SelectGridAsync(size);
        var secondGrid = _sut.SelectedGrid;

        // Assert
        firstGrid.Should().NotBeNull();
        secondGrid.Should().BeSameAs(firstGrid);
    }

    [Fact]
    public async Task SelectGridAsync_ShouldDoNothing_WhenNoImageLoaded()
    {
        // Act
        await _sut.SelectGridAsync(5);

        // Assert
        _sut.SelectedGrid.Should().BeNull();
    }

    [Fact]
    public async Task CreateVersionSnapshotAsync_WhenGridLoaded_AddsVersionAndMarksDirty()
    {
        using var stream = CreateTestImageStream(10, 10);
        await _sut.LoadImageAsync(stream);

        var countBefore = _sut.Versions.Count;
        var snapshot = await _sut.CreateVersionSnapshotAsync("Test Snapshot");

        snapshot.Should().NotBeNull();
        snapshot.Name.Should().Be("Test Snapshot");
        _sut.Versions.Count.Should().Be(countBefore + 1);
        _sut.ActiveVersion.Should().Be(snapshot);
        _sut.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void MarkDirty_UpdatesIsDirty()
    {
        _sut.IsDirty = false;
        _sut.MarkDirty(true);
        _sut.IsDirty.Should().BeTrue();
        _sut.MarkDirty(false);
        _sut.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task LoadImageAsync_ShouldPopulateInitialSourceImageHistory()
    {
        using var stream = CreateTestImageStream(30, 40);
        await _sut.LoadImageAsync(stream);

        _sut.SourceImageHistory.Should().HaveCount(1);
        var initial = _sut.SourceImageHistory[0];
        initial.Name.Should().Be("Original Image");
        initial.Width.Should().Be(30);
        initial.Height.Should().Be(40);
        _sut.ActiveSourceImageVersion.Should().Be(initial);
        _sut.HasSourceImage.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyDoctoredImageAsync_ShouldAddNewSourceImageVersion_AndReSliceGrid()
    {
        using var origStream = CreateTestImageStream(30, 30);
        await _sut.LoadImageAsync(origStream);

        using var doctoredStream = CreateTestImageStream(20, 20);
        await _sut.ApplyDoctoredImageAsync(doctoredStream, "Cropped 20x20");

        _sut.SourceImageHistory.Should().HaveCount(2);
        var second = _sut.SourceImageHistory[1];
        second.Name.Should().Be("Cropped 20x20");
        second.Width.Should().Be(20);
        second.Height.Should().Be(20);
        _sut.ActiveSourceImageVersion.Should().Be(second);
        _sut.IsDirty.Should().BeTrue();
        _sut.SelectedGrid.Should().NotBeNull();
        _sut.Versions.Should().Contain(v => v.Name.Contains("Cropped 20x20"));
    }

    [Fact]
    public async Task SwitchToSourceImageVersionAsync_ShouldSwitchActiveVersion_AndReSlice()
    {
        using var origStream = CreateTestImageStream(40, 40);
        await _sut.LoadImageAsync(origStream);

        var origVersionId = _sut.ActiveSourceImageVersion!.Id;

        using var doctoredStream = CreateTestImageStream(20, 20);
        await _sut.ApplyDoctoredImageAsync(doctoredStream, "Doctored");

        _sut.ActiveSourceImageVersion!.Name.Should().Be("Doctored");

        // Now switch back to original
        await _sut.SwitchToSourceImageVersionAsync(origVersionId);

        _sut.ActiveSourceImageVersion.Should().NotBeNull();
        _sut.ActiveSourceImageVersion!.Id.Should().Be(origVersionId);
        _sut.ActiveSourceImageVersion.Name.Should().Be("Original Image");
        _sut.SelectedGrid.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadImageAsync_WithSourceImageName_ShouldSetSourceImageName_AndProjectName_AndHasSavedProjectFalse()
    {
        using var stream = CreateTestImageStream(30, 30);
        await _sut.LoadImageAsync(stream, "landscape_sunset.png");

        _sut.SourceImageName.Should().Be("landscape_sunset.png");
        _sut.ProjectName.Should().Be("landscape_sunset");
        _sut.HasSavedProject.Should().BeFalse();
        _sut.CurrentFilePath.Should().BeNull();
    }

    [Fact]
    public async Task SaveProjectAsync_ShouldUpdateProjectNameFromFilePath_AndSetHasSavedProjectTrue()
    {
        using var stream = CreateTestImageStream(20, 20);
        await _sut.LoadImageAsync(stream, "raw_camera_img.jpg");

        _sut.HasSavedProject.Should().BeFalse();

        string tempPath = Path.Combine(Path.GetTempPath(), $"MyCraftProject_{Guid.NewGuid():N}.drillmagic");
        try
        {
            await _sut.SaveProjectAsync(tempPath);

            _sut.HasSavedProject.Should().BeTrue();
            _sut.CurrentFilePath.Should().Be(tempPath);
            _sut.ProjectName.Should().StartWith("MyCraftProject_");
            _sut.CurrentProject!.Manifest.Name.Should().StartWith("MyCraftProject_");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task LoadImageAsync_WithHighResolutionImage_ShouldSupportCellSizesUpTo150()
    {
        // High resolution image (300x300) should populate available cell sizes up to 150
        using var stream = CreateTestImageStream(300, 300);
        await _sut.LoadImageAsync(stream);

        _sut.AvailableCellSizes.Should().NotBeEmpty();
        _sut.AvailableCellSizes.Max().Should().Be(150);
        _sut.AvailableCellSizes.Should().HaveCount(150);
    }

    [Fact]
    public async Task EnforceMinimumColorCountAsync_ShouldCreateSnapshotAndMarkDirty()
    {
        using var stream = CreateTestImageStream(30, 30);
        await _sut.LoadImageAsync(stream);

        _sut.SelectedGrid.Should().NotBeNull();
        // Introduce a speckle color
        _sut.SelectedGrid!.Grid[0, 0] = System.Drawing.Color.FromArgb(123, 45, 67);
        _sut.SelectedGrid.RecalculateColorSummary();

        int initialVersions = _sut.Versions.Count;
        int eliminated = await _sut.EnforceMinimumColorCountAsync(5);

        eliminated.Should().BeGreaterThanOrEqualTo(1);
        _sut.IsDirty.Should().BeTrue();
        _sut.Versions.Count.Should().Be(initialVersions + 1);
        _sut.ActiveVersion!.Name.Should().Contain("Min Count Threshold");
    }

    [Fact]
    public async Task SelectGridByDimensionsAsync_ShouldCreateGridWithTargetDimensions_AndSnapshot()
    {
        // 200x160 image, target 40x30 drills
        // 200/40 = 5, 160/30 = 5.33 -> cell size 5
        using var stream = CreateTestImageStream(200, 160);
        await _sut.LoadImageAsync(stream);

        int initialVersions = _sut.Versions.Count;
        await _sut.SelectGridByDimensionsAsync(40, 30);

        _sut.SelectedGrid.Should().NotBeNull();
        _sut.SelectedGrid!.Width.Should().Be(40);
        _sut.SelectedGrid.Height.Should().Be(30);
        _sut.SelectedGrid.CellSize.Should().Be(5);
        _sut.IsDirty.Should().BeTrue();
        _sut.Versions.Count.Should().Be(initialVersions + 1);
        _sut.ActiveVersion!.Name.Should().Contain("40×30 drills");
    }

    [Fact]
    public async Task AnalyzeTargetDimensions_ShouldReturnAccurateShavingAnalysis()
    {
        using var stream = CreateTestImageStream(100, 80);
        await _sut.LoadImageAsync(stream);

        _sut.SourceImageWidth.Should().Be(100);
        _sut.SourceImageHeight.Should().Be(80);

        var analysis = _sut.AnalyzeTargetDimensions(10, 8);
        analysis.Should().NotBeNull();
        analysis!.EffectiveCellSize.Should().Be(10);
        analysis.HasShaving.Should().BeFalse();
        analysis.IsExceedingResolution.Should().BeFalse();

        var shavedAnalysis = _sut.AnalyzeTargetDimensions(15, 8);
        shavedAnalysis.Should().NotBeNull();
        shavedAnalysis!.EffectiveCellSize.Should().Be(6); // 100/15 = 6, 80/8 = 10 -> min is 6
        shavedAnalysis.HasShaving.Should().BeTrue();
    }
}