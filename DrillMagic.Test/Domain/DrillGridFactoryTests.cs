using System.IO;
using DrillMagic.Core;
using DrillMagic.Core.Types;
using FluentAssertions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using DrawingColor = System.Drawing.Color;
using System;

namespace DrillMagic.Test.Domain;

public class DrillGridFactoryTests
{
    private readonly Mock<IColorMapService> _mockColorMapService;
    private readonly DrillGridFactory _factory;

    public DrillGridFactoryTests()
    {
        _mockColorMapService = new Mock<IColorMapService>();
        _factory = new DrillGridFactory(_mockColorMapService.Object);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenColorMapServiceIsNull()
    {
        Action act = () => new DrillGridFactory(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("colorMapService");
    }

    [Fact]
    public void CreateGrid_ShouldReturnInitializedGrid()
    {
        // Arrange
        int height = 10;
        int width = 20;
        uint cellSize = 5;

        // Act
        var grid = _factory.CreateGrid(height, width, cellSize);

        // Assert
        grid.Should().NotBeNull();
        grid.Height.Should().Be(height);
        grid.Width.Should().Be(width);
        grid.CellSize.Should().Be(cellSize);
    }

    [Fact]
    public void CreateGridFromImage_ShouldReturnInitializedGrid_FromImageStream()
    {
        // Arrange
        uint cellSize = 1; // 1x1 cells
        using var image = new Image<Rgba32>(10, 10);
        // Fill with some color to be safe (set 0,0 to opaque black)
        image[0, 0] = new Rgba32(0, 0, 0, 255);
        
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0; // Reset position

        // Mock FindClosestDMCColor to return something valid when DrillGrid calls it
        _mockColorMapService.Setup(s => s.FindClosestDMCColor(It.IsAny<DrawingColor>()))
            .Returns(new DMCColor("Black", 310, "#000000"));

        // Act
        var grid = _factory.CreateGridFromImage(stream, cellSize);

        // Assert
        grid.Should().NotBeNull();
        grid.Height.Should().Be(10); // 10/1
        grid.Width.Should().Be(10);
        grid.CellSize.Should().Be(cellSize);
    }
}
