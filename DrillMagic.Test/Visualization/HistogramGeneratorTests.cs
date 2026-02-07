using System;
using System.Drawing;
using DrillMagic.Core;
using DrillMagic.Core.Visualization;
using DrillMagic.Core.Types;
using Moq;
using Xunit;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DrillMagic.Test.Visualization;

public class HistogramGeneratorTests
{
    private readonly Mock<IColorMapService> _mockService;
    private readonly DrillGrid _grid;
    private readonly HistogramGenerator _generator;

    public HistogramGeneratorTests()
    {
        _mockService = new Mock<IColorMapService>();
        _grid = new DrillGrid(_mockService.Object, 10, 10);
        _generator = new HistogramGenerator();
    }

    [Fact]
    public void GenerateHistogram_ShouldThrowArgumentNullException_WhenGridIsNull()
    {
        Action act = () => _generator.GenerateHistogram(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("grid");
    }

    [Fact]
    public void GenerateHistogram_ShouldReturnWhiteEmptyImage_WhenGridIsEmpty()
    {
        // Act
        using var image = _generator.GenerateHistogram(_grid);

        // Assert
        image.Should().NotBeNull();
        image.Width.Should().Be(800);
        image.Height.Should().Be(400);

        // Check a pixel to ensure it's white (background)
        image[0, 0].Should().Be(new Rgba32(255, 255, 255, 255));
    }

    [Fact]
    public void GenerateHistogram_ShouldDrawBars_WhenGridHasData()
    {
        // Arrange
        // Manually populate grid
        // 5 Red pixels
        // 10 Blue pixels
        for(int i=0; i<5; i++) _grid.Grid[0, i] = System.Drawing.Color.Red;
        for(int i=0; i<10; i++) _grid.Grid[1, i] = System.Drawing.Color.Blue;
        
        _grid.RecalculateColorSummary();

        // Act
        using var image = _generator.GenerateHistogram(_grid);

        // Assert
        image.Should().NotBeNull();
        
        // We can't easily verify the drawing content without complex image analysis,
        // but we can verify it didn't throw and returned a valid image.
        // And optionally check that some pixels are NOT white (bars drawn).
        
        // Center of where Red bar should be? 
        // 2 bars total. Width 800. Margin 20. ChartWidth 760. BarWidth 380.
        // Sorted: Blue (10), Red (5).
        // Blue at x=20..400. Red at x=400..780.
        // Blue is roughly full height (10/10). Red is half height.
        
        // Sample pixel in blue bar area
        // x=100, y=300 (bottom area)
        var pixel = image[100, 300];
        // Note: Drawing uses antialiasing by default maybe, and border.
        // But center should be Blue.
        // Comparing Rgba32 to Blue.
        pixel.B.Should().Be(255);
        pixel.R.Should().Be(0);
    }
}
