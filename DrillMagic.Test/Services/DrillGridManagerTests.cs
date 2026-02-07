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
        var image = new Image<Rgba32>(width, height);
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
        
        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return stream;
    }
}