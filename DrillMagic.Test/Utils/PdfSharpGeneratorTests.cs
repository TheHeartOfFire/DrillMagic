using DrillMagic.Core;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Utils;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace DrillMagic.Test.Utils;

public class PdfSharpGeneratorTests
{
    private readonly Mock<IColorMapService> _colorMapServiceMock;
    private readonly Mock<ILogger<PdfSharpGenerator>> _loggerMock;
    private readonly PdfSharpGenerator _sut;

    public PdfSharpGeneratorTests()
    {
        _colorMapServiceMock = new Mock<IColorMapService>();
        _loggerMock = new Mock<ILogger<PdfSharpGenerator>>();
        
        // Mock GetDMCColor to return a default object, otherwise it returns null causing NRE
        _colorMapServiceMock
            .Setup(s => s.GetDMCColor(It.IsAny<System.Drawing.Color>()))
            .Returns(DMCColor.Empty);

        _sut = new PdfSharpGenerator(_colorMapServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateDrillGridPdf_WithValidGrid_ShouldReturnPdfBytes()
    {
        // Arrange
        // Using minimal valid parameters for DrillGrid
        int height = 10;
        int width = 10;
        uint cellSize = 3;
        var grid = new DrillGrid(_colorMapServiceMock.Object, height, width, cellSize);

        // Act
        var result = await _sut.GenerateDrillGridPdf(grid);

        // Assert
        result.ErrorMessage.Should().BeNull();
        result.PdfBytes.Should().NotBeNull();
        result.PdfBytes.Should().NotBeEmpty("PDF bytes should be generated even for an empty grid");
    }
    
    [Fact]
    public async Task GenerateDrillGridPdf_WithLargeGrid_ShouldGenerateMultiplePages()
    {
        // Arrange
        // A standard page is approx 70x90 drills at 3mm.
        // Create a 200x200 grid to force multi-page tiling.
        int height = 200;
        int width = 200;
        uint cellSize = 3;
        var grid = new DrillGrid(_colorMapServiceMock.Object, height, width, cellSize);
        
        // Populate with a single color
        var testColor = System.Drawing.Color.Red;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                grid.Grid[y, x] = testColor;
            }
        }
        grid.RecalculateColorSummary();

        // Act
        var result = await _sut.GenerateDrillGridPdf(grid);

        // Assert
        result.ErrorMessage.Should().BeNull();
        result.PdfBytes.Should().NotBeNull();
        result.PdfBytes.Length.Should().BeGreaterThan(1000, "Big PDF should be generated");
    }

    [Fact]
    public async Task GenerateDrillGridPdf_WithManyColors_ShouldGenerateComplexLegend()
    {
        // Arrange
        int height = 50;
        int width = 50;
        var grid = new DrillGrid(_colorMapServiceMock.Object, height, width, 3);
        
        // Populate with many different colors to stress legend layout
        // Max ~60-70 symbols in the symbol set?
        // Let's create 50 distinct colors
        for (int i = 0; i < 50; i++)
        {
            var color = System.Drawing.Color.FromArgb(255, (byte)(i * 4), (byte)(255 - i * 4), 100);
            grid.Grid[i, i] = color; // Diagonal line of unique colors
        }
        grid.RecalculateColorSummary();

        // Act
        var result = await _sut.GenerateDrillGridPdf(grid);

        // Assert
        result.ErrorMessage.Should().BeNull();
        result.PdfBytes.Should().NotBeNull();
    }
}