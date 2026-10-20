using DrillMagic.Core.Sizing;
using Xunit;

namespace DrillMagic.Test.Sizing;

public class PhysicalDimensionsCalculatorTests
{
    [Fact]
    public void CalculateDimensions_SquareDrills_ReturnsCorrectCmAndInches()
    {
        // 100 square drills = 100 * 2.5mm = 250mm = 25.0 cm = ~9.8425 inches
        // 200 square drills = 200 * 2.5mm = 500mm = 50.0 cm = ~19.685 inches
        var (wCm, hCm, wIn, hIn) = PhysicalDimensionsCalculator.CalculateDimensions(100, 200, DrillShape.Square);

        Assert.Equal(25.0, wCm, 1);
        Assert.Equal(50.0, hCm, 1);
        Assert.Equal(9.84, wIn, 2);
        Assert.Equal(19.69, hIn, 2);
    }

    [Fact]
    public void CalculateDimensions_RoundDrills_ReturnsCorrectCmAndInches()
    {
        // 100 round drills = 100 * 2.8mm = 280mm = 28.0 cm = ~11.0236 inches
        // 200 round drills = 200 * 2.8mm = 560mm = 56.0 cm = ~22.0472 inches
        var (wCm, hCm, wIn, hIn) = PhysicalDimensionsCalculator.CalculateDimensions(100, 200, DrillShape.Round);

        Assert.Equal(28.0, wCm, 1);
        Assert.Equal(56.0, hCm, 1);
        Assert.Equal(11.02, wIn, 2);
        Assert.Equal(22.05, hIn, 2);
    }

    [Fact]
    public void FormatDimensions_FormatsExpectedString()
    {
        var text = PhysicalDimensionsCalculator.FormatDimensions(160, 200, DrillShape.Square);

        // 160 * 0.25 = 40.0 cm (15.7"), 200 * 0.25 = 50.0 cm (19.7")
        Assert.Contains("40.0 × 50.0 cm", text);
        Assert.Contains("15.7″ × 19.7″", text);
    }

    [Fact]
    public void FormatDimensions_ZeroOrNegative_ReturnsZeroString()
    {
        var text = PhysicalDimensionsCalculator.FormatDimensions(0, 0, DrillShape.Square);
        Assert.Equal("0.0 × 0.0 cm", text);
    }

    [Fact]
    public void CalculateProjectedGridSize_ComputesExpectedDrillCounts()
    {
        // Image 800x600 with cell size 10 -> 80x60 drills
        var (w, h) = PhysicalDimensionsCalculator.CalculateProjectedGridSize(800, 600, 10);
        Assert.Equal(80, w);
        Assert.Equal(60, h);

        // Image 805x605 with cell size 10 -> ceiling 81x61 drills
        var (w2, h2) = PhysicalDimensionsCalculator.CalculateProjectedGridSize(805, 605, 10);
        Assert.Equal(81, w2);
        Assert.Equal(61, h2);
    }
}

