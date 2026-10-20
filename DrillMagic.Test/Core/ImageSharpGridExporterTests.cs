using DrillMagic.Core;
using DrillMagic.Core.Export;
using DrillMagic.Core.Types;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Threading.Tasks;
using Xunit;

namespace DrillMagic.Test.Core;

public class ImageSharpGridExporterTests
{
    private readonly Mock<IColorMapService> _colorMapServiceMock;
    private readonly ImageSharpGridExporter _sut;

    public ImageSharpGridExporterTests()
    {
        _colorMapServiceMock = new Mock<IColorMapService>();
        _sut = new ImageSharpGridExporter();
    }

    [Fact]
    public async Task ExportToPngAsync_ValidGrid_ReturnsValidPngBytes()
    {
        var gridMatrix = new Color[4, 6];
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                gridMatrix[y, x] = Color.FromArgb(255, 100, 150, 200);
            }
        }

        var grid = new DrillGrid(_colorMapServiceMock.Object, gridMatrix, cellSize: 10);
        var options = new ImageExportOptions { PixelsPerCell = 10, IncludeGridLines = true };

        var pngBytes = await _sut.ExportToPngAsync(grid, options);

        pngBytes.Should().NotBeNullOrEmpty();
        // Check PNG file magic header: 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A
        pngBytes.Length.Should().BeGreaterThan(8);
        pngBytes[0].Should().Be(0x89);
        pngBytes[1].Should().Be(0x50);
        pngBytes[2].Should().Be(0x4E);
        pngBytes[3].Should().Be(0x47);
    }
}

