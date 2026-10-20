using DrillMagic.Core;
using DrillMagic.Core.Projects;
using DrillMagic.Core.Types;
using FluentAssertions;
using Moq;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DrillMagic.Test.Core;

public class ZipProjectPackageServiceTests
{
    private readonly Mock<IColorMapService> _colorMapServiceMock;
    private readonly Mock<IDrillGridFactory> _gridFactoryMock;
    private readonly ZipProjectPackageService _sut;

    public ZipProjectPackageServiceTests()
    {
        _colorMapServiceMock = new Mock<IColorMapService>();
        _colorMapServiceMock.Setup(s => s.GetDMCColor(It.IsAny<Color>()))
            .Returns((Color c) => new DMCColor("Red", 666, "#FFFF0000"));

        _gridFactoryMock = new Mock<IDrillGridFactory>();
        _gridFactoryMock.Setup(f => f.CreateGrid(It.IsAny<Color[,]>(), It.IsAny<uint>(), It.IsAny<float>(), It.IsAny<float>()))
            .Returns((Color[,] g, uint cs, float rot, float stg) => new DrillGrid(_colorMapServiceMock.Object, g, cs, rot, stg));

        _sut = new ZipProjectPackageService();
    }

    [Fact]
    public async Task SaveAsync_And_LoadAsync_RoundtripsProjectWithMultipleVersions()
    {
        var package = new DrillProjectPackage
        {
            Manifest = new ProjectManifest
            {
                Name = "Test Mosaic",
                ActiveVersionId = "v2",
                Versions =
                [
                    new ProjectVersionSummary
                    {
                        Id = "v1",
                        Name = "Draft",
                        CellSize = 10,
                        Width = 2,
                        Height = 2,
                        ColorCount = 1,
                        DataFile = "versions/v1.json"
                    },
                    new ProjectVersionSummary
                    {
                        Id = "v2",
                        Name = "Final",
                        CellSize = 12,
                        Width = 2,
                        Height = 2,
                        ColorCount = 1,
                        DataFile = "versions/v2.json"
                    }
                ]
            },
            SourceImageBytes = [1, 2, 3, 4, 5],
            MainThumbnail = [9, 8, 7]
        };

        var v1Data = new ProjectVersionData
        {
            Id = "v1",
            Name = "Draft",
            CellSize = 10,
            Width = 2,
            Height = 2,
            Palette = [new PaletteEntry { PaletteIndex = 0, Argb = "#FFFF0000", DmcNumber = 666, Name = "Red" }],
            Cells = [[0, 4]]
        };

        var v2Data = new ProjectVersionData
        {
            Id = "v2",
            Name = "Final",
            CellSize = 12,
            Width = 2,
            Height = 2,
            Palette = [new PaletteEntry { PaletteIndex = 0, Argb = "#FFFF0000", DmcNumber = 666, Name = "Red" }],
            Cells = [[0, 4]]
        };

        package.VersionsData["v1"] = v1Data;
        package.VersionsData["v2"] = v2Data;

        using var memoryStream = new MemoryStream();
        await _sut.SaveAsync(package, memoryStream);

        memoryStream.Position = 0;
        var loaded = await _sut.LoadAsync(memoryStream);

        loaded.Manifest.Name.Should().Be("Test Mosaic");
        loaded.Manifest.ActiveVersionId.Should().Be("v2");
        loaded.Manifest.Versions.Should().HaveCount(2);
        loaded.SourceImageBytes.Should().Equal(new byte[] { 1, 2, 3, 4, 5 });
        loaded.MainThumbnail.Should().Equal(new byte[] { 9, 8, 7 });
        loaded.VersionsData.Should().ContainKey("v1");
        loaded.VersionsData.Should().ContainKey("v2");
        loaded.VersionsData["v2"].CellSize.Should().Be(12);
    }

    [Fact]
    public void CreateVersionData_And_RestoreGridFromVersion_CorrectlyRecreatesGrid()
    {
        var grid = new Color[2, 3];
        grid[0, 0] = Color.FromArgb(255, 255, 0, 0);
        grid[0, 1] = Color.FromArgb(255, 255, 0, 0);
        grid[0, 2] = Color.FromArgb(255, 255, 0, 0);
        grid[1, 0] = Color.FromArgb(255, 255, 0, 0);
        grid[1, 1] = Color.FromArgb(255, 255, 0, 0);
        grid[1, 2] = Color.FromArgb(255, 255, 0, 0);

        var originalGrid = new DrillGrid(_colorMapServiceMock.Object, grid, cellSize: 15, rotation: 5f, normalizedStaggerOffset: 0.5f);

        var versionData = _sut.CreateVersionData("v1", "Test Snapshot", originalGrid, _colorMapServiceMock.Object);

        versionData.Id.Should().Be("v1");
        versionData.Name.Should().Be("Test Snapshot");
        versionData.CellSize.Should().Be(15);
        versionData.Width.Should().Be(3);
        versionData.Height.Should().Be(2);

        var restored = _sut.RestoreGridFromVersion(versionData, _gridFactoryMock.Object);
        restored.Width.Should().Be(3);
        restored.Height.Should().Be(2);
        restored.CellSize.Should().Be(15);
        restored.Rotation.Should().Be(5f);
        restored.NormalizedStaggerOffset.Should().Be(0.5f);
    }

    [Fact]
    public async Task SaveAsync_And_LoadAsync_RoundtripsSourceImageHistory()
    {
        var package = new DrillProjectPackage
        {
            Manifest = new ProjectManifest
            {
                Name = "Image History Project",
                ActiveSourceImageVersionId = "img2",
                SourceImageHistory =
                [
                    new SourceImageVersionSummary
                    {
                        Id = "img1",
                        Name = "Original Image",
                        Width = 100,
                        Height = 100,
                        ByteSize = 5,
                        ImageFileName = "assets/images/img1.png"
                    },
                    new SourceImageVersionSummary
                    {
                        Id = "img2",
                        Name = "Doctored Crop",
                        Width = 80,
                        Height = 80,
                        ByteSize = 3,
                        ImageFileName = "assets/images/img2.png"
                    }
                ]
            },
            SourceImageBytes = [10, 20, 30]
        };

        package.SourceImageVersionsData["img1"] = [1, 2, 3, 4, 5];
        package.SourceImageVersionsData["img2"] = [10, 20, 30];

        using var memoryStream = new MemoryStream();
        await _sut.SaveAsync(package, memoryStream);

        memoryStream.Position = 0;
        var loaded = await _sut.LoadAsync(memoryStream);

        loaded.Manifest.ActiveSourceImageVersionId.Should().Be("img2");
        loaded.Manifest.SourceImageHistory.Should().HaveCount(2);
        loaded.Manifest.SourceImageHistory[0].Name.Should().Be("Original Image");
        loaded.Manifest.SourceImageHistory[1].Name.Should().Be("Doctored Crop");
        loaded.SourceImageVersionsData.Should().ContainKey("img1");
        loaded.SourceImageVersionsData["img1"].Should().Equal(new byte[] { 1, 2, 3, 4, 5 });
        loaded.SourceImageVersionsData.Should().ContainKey("img2");
        loaded.SourceImageVersionsData["img2"].Should().Equal(new byte[] { 10, 20, 30 });
    }
}

