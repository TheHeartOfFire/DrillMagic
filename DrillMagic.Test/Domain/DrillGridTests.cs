using FluentAssertions;
using Xunit;
using Moq;
using DrillMagic.Core;
using DrillMagic.Core.Types;
using System.Drawing;
using System.Collections.Generic;

namespace DrillMagic.Test.Domain;

public class DrillGridTests
{
    private readonly Mock<IColorMapService> _mockColorMapService;

    public DrillGridTests()
    {
        _mockColorMapService = new Mock<IColorMapService>();
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties_WhenGivenValidDimensions()
    {
        // Arrange
        int height = 100;
        int width = 200;
        uint cellSize = 3;

        // Act
        var grid = new DrillGrid(_mockColorMapService.Object, height, width, cellSize);

        // Assert
        grid.Width.Should().Be(width);
        grid.Height.Should().Be(height);
        grid.CellSize.Should().Be(cellSize);
        grid.Grid.Should().NotBeNull();
        grid.Grid.GetLength(0).Should().Be(height);
        grid.Grid.GetLength(1).Should().Be(width);
        grid.ColorSummary.Should().BeEmpty();
    }

    [Fact]
    public void RecalculateColorSummary_ShouldCountColorsCorrectly()
    {
        // Arrange
        var grid = new DrillGrid(_mockColorMapService.Object, 10, 10);
        grid.Grid[0, 0] = Color.Red;
        grid.Grid[0, 1] = Color.Red;
        grid.Grid[0, 2] = Color.Blue;

        // Act
        grid.RecalculateColorSummary();

        // Assert
        grid.ColorSummary.Should().ContainKey(Color.Red).WhoseValue.Should().Be(2);
        grid.ColorSummary.Should().ContainKey(Color.Blue).WhoseValue.Should().Be(1);
        grid.ColorSummary.Count.Should().Be(2);
    }

    [Fact]
    public void ReduceColors_ShouldReduceNumberOfColors_WhenTargetIsLowerThanCurrent()
    {
        // Arrange
        var grid = new DrillGrid(_mockColorMapService.Object, 10, 10);
        
        // Setup 3 colors
        // c1: Count 3
        var c1 = Color.FromArgb(100, 100, 100); 
        // c2: Count 1 (Close to c1, should be merged into c1)
        var c2 = Color.FromArgb(102, 102, 102); 
        // c3: Count 2 (Far from c1/c2, should be kept)
        var c3 = Color.FromArgb(200, 200, 200); 
        
        grid.Grid[0, 0] = c1;
        grid.Grid[0, 1] = c1;
        grid.Grid[0, 2] = c1; 
        
        grid.Grid[1, 0] = c2; 
        
        grid.Grid[2, 0] = c3; 
        grid.Grid[2, 1] = c3; 
        
        grid.RecalculateColorSummary();
        
        // Act
        grid.ReduceColors(2);

        // Assert
        grid.ColorSummary.Count.Should().Be(2);
        grid.ColorSummary.Should().ContainKey(c1); // Kept (higher count than c2)
        grid.ColorSummary.Should().ContainKey(c3); // Kept (distance)
        grid.ColorSummary.Should().NotContainKey(c2); // Merged into c1
        
        // Verify replacement in grid
        grid.Grid[1, 0].Should().Be(c1);
    }

    [Fact]
    public void GetClosestDMCColor_ShouldReturnOriginalColor_WhenNoMatchFound()
    {
        // Arrange
        // Simulate finding nothing (e.g., empty repository)
        _mockColorMapService.Setup(s => s.FindClosestDMCColor(It.IsAny<Color>()))
            .Returns(DMCColor.Empty);

        var grid = new DrillGrid(_mockColorMapService.Object, 10, 10);
        var inputColor = Color.Magenta;

        // Act
        var result = grid.GetClosestDMCColor(inputColor);

        // Assert
        result.Should().Be(inputColor);
    }

    [Fact]
    public void GetClosestDMCColor_ShouldReturnCachedResult_OnSecondCall()
    {
        // Arrange
        var inputColor = Color.Magenta;
        // Use ARGB to match what DMCColor returns via ColorTranslator.FromHtml
        var matchedColor = Color.FromArgb(255, 0, 0, 0); 
        var dmc = new DMCColor("Black", 310, "#000000"); // Black matches usually

        _mockColorMapService.Setup(s => s.FindClosestDMCColor(inputColor))
            .Returns(dmc);

        var grid = new DrillGrid(_mockColorMapService.Object, 10, 10);

        // Act
        var result1 = grid.GetClosestDMCColor(inputColor);
        var result2 = grid.GetClosestDMCColor(inputColor);

        // Assert
        result1.ToArgb().Should().Be(matchedColor.ToArgb());
        result2.ToArgb().Should().Be(matchedColor.ToArgb());

        // Verify service was called only once due to caching/buffering
        _mockColorMapService.Verify(s => s.FindClosestDMCColor(inputColor), Times.Once);
    }
    
    [Fact]
    public void ReduceColors_ShouldDoNothing_WhenCountIsAlreadyLow()
    {
        // Arrange
        var grid = new DrillGrid(_mockColorMapService.Object, 10, 10);
        grid.Grid[0,0] = Color.Red;
        grid.RecalculateColorSummary(); // Count = 1

        // Act
        grid.ReduceColors(2);

        // Assert
        grid.ColorSummary.Count.Should().Be(1);
    }

    [Fact]
    public void GetColorsBelowThresholdCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var grid = new DrillGrid(_mockColorMapService.Object, 10, 10);
        var c1 = Color.FromArgb(255, 0, 0); // Count 3
        var c2 = Color.FromArgb(0, 255, 0); // Count 1
        var c3 = Color.FromArgb(0, 0, 255); // Count 2

        grid.Grid[0, 0] = c1;
        grid.Grid[0, 1] = c1;
        grid.Grid[0, 2] = c1;
        grid.Grid[1, 0] = c2;
        grid.Grid[2, 0] = c3;
        grid.Grid[2, 1] = c3;

        grid.RecalculateColorSummary();

        // Act & Assert
        grid.GetColorsBelowThresholdCount(1).Should().Be(0); // 1 or less returns 0
        grid.GetColorsBelowThresholdCount(2).Should().Be(1); // c2 has 1 < 2
        grid.GetColorsBelowThresholdCount(3).Should().Be(2); // c2 (1) and c3 (2) < 3
        grid.GetColorsBelowThresholdCount(4).Should().Be(3); // all 3 colors < 4
    }

    [Fact]
    public void EnforceMinimumColorCount_ShouldMergeRareColorsIntoClosestColor()
    {
        // Arrange
        var grid = new DrillGrid(_mockColorMapService.Object, 10, 10);

        var c1 = Color.FromArgb(100, 100, 100); // 5 instances
        var c2 = Color.FromArgb(102, 102, 102); // 1 instance (closest to c1)
        var c3 = Color.FromArgb(250, 250, 250); // 10 instances (far from c1 and c2)

        for (int i = 0; i < 5; i++) grid.Grid[0, i] = c1;
        grid.Grid[1, 0] = c2;
        for (int i = 0; i < 10; i++) grid.Grid[2, i] = c3;

        grid.RecalculateColorSummary();
        grid.ColorSummary.Count.Should().Be(3);

        // Act - threshold 3: c2 (count 1) should be merged into closest color c1
        int eliminated = grid.EnforceMinimumColorCount(3);

        // Assert
        eliminated.Should().Be(1);
        grid.ColorSummary.Count.Should().Be(2);
        grid.ColorSummary.Should().ContainKey(c1);
        grid.ColorSummary.Should().ContainKey(c3);
        grid.ColorSummary.Should().NotContainKey(c2);
        grid.ColorSummary[c1].Should().Be(6); // 5 + 1
        grid.ColorSummary[c3].Should().Be(10);
        grid.Grid[1, 0].Should().Be(c1); // cell was updated
    }

    [Fact]
    public void EnforceMinimumColorCount_ShouldPreserveAtLeastOneColor_WhenAllBelowThreshold()
    {
        // Arrange: 2 colors with count 1 each, threshold 10
        var grid = new DrillGrid(_mockColorMapService.Object, 5, 5);
        var c1 = Color.Red;
        var c2 = Color.Blue;
        grid.Grid[0, 0] = c1;
        grid.Grid[0, 1] = c2;

        grid.RecalculateColorSummary();
        grid.ColorSummary.Count.Should().Be(2);

        // Act
        int eliminated = grid.EnforceMinimumColorCount(10);

        // Assert: 1 color remains, not 0
        eliminated.Should().Be(1);
        grid.ColorSummary.Count.Should().Be(1);
        grid.ColorSummary.Values.First().Should().Be(2);
    }

    [Fact]
    public void EnforceMinimumColorCount_ShouldReturnZero_WhenThresholdIsOneOrLess()
    {
        // Arrange
        var grid = new DrillGrid(_mockColorMapService.Object, 5, 5);
        grid.Grid[0, 0] = Color.Red;
        grid.RecalculateColorSummary();

        // Act
        int eliminated = grid.EnforceMinimumColorCount(1);

        // Assert
        eliminated.Should().Be(0);
    }
}

