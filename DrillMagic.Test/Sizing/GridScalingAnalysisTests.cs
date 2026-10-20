using DrillMagic.Core.Sizing;
using FluentAssertions;
using Xunit;

namespace DrillMagic.Test.Sizing;

public class GridScalingAnalysisTests
{
    [Fact]
    public void Analyze_ExactFit_ShouldHaveZeroShaving_AndFullRepresentation()
    {
        // 1000x800 image, target 100x80 drills -> cellSize 10, exact fit
        var analysis = GridScalingAnalysis.Analyze(1000, 800, 100, 80);

        analysis.EffectiveCellSize.Should().Be(10);
        analysis.UsedWidthPixels.Should().Be(1000);
        analysis.UsedHeightPixels.Should().Be(800);
        analysis.ShavedWidthPixels.Should().Be(0);
        analysis.ShavedHeightPixels.Should().Be(0);
        analysis.HasShaving.Should().BeFalse();
        analysis.IsExceedingResolution.Should().BeFalse();
        analysis.FormatAlertMessage().Should().Contain("100% of source pixels are covered");
    }

    [Fact]
    public void Analyze_WithHorizontalAndVerticalShaving_ShouldCalculatePixelsAndPercentages()
    {
        // 1920x1080 image, target 100x75 drills
        // 1920 / 100 = 19; 1080 / 75 = 14 -> effectiveCellSize = 14
        // Used: 100*14 = 1400; 75*14 = 1050
        // Shaved: 1920 - 1400 = 520 (260 left, 260 right); 1080 - 1050 = 30 (15 top, 15 bottom)
        var analysis = GridScalingAnalysis.Analyze(1920, 1080, 100, 75);

        analysis.EffectiveCellSize.Should().Be(14);
        analysis.UsedWidthPixels.Should().Be(1400);
        analysis.UsedHeightPixels.Should().Be(1050);
        analysis.ShavedWidthPixels.Should().Be(520);
        analysis.ShavedHeightPixels.Should().Be(30);
        analysis.ShavedLeftRightEach.Should().Be(260);
        analysis.ShavedTopBottomEach.Should().Be(15);
        analysis.StartOffsetX.Should().Be(260);
        analysis.StartOffsetY.Should().Be(15);
        analysis.HasShaving.Should().BeTrue();
        analysis.IsExceedingResolution.Should().BeFalse();

        var message = analysis.FormatAlertMessage();
        message.Should().Contain("520 px horizontally");
        message.Should().Contain("30 px vertically");
        message.Should().Contain("260 px left/right");
        message.Should().Contain("15 px top/bottom");
    }

    [Fact]
    public void Analyze_WhenTargetExceedsResolution_ShouldMarkExceeding()
    {
        // 500x400 image, target 1000x800 drills
        var analysis = GridScalingAnalysis.Analyze(500, 400, 1000, 800);

        analysis.IsExceedingResolution.Should().BeTrue();
        analysis.EffectiveCellSize.Should().Be(0);
        analysis.FormatAlertMessage().Should().Contain("exceeds source image resolution");
    }

    [Fact]
    public void Analyze_WithInvalidDimensions_ShouldMarkExceeding()
    {
        var analysis = GridScalingAnalysis.Analyze(0, 100, 10, 10);
        analysis.IsExceedingResolution.Should().BeTrue();

        var analysis2 = GridScalingAnalysis.Analyze(100, 100, -5, 10);
        analysis2.IsExceedingResolution.Should().BeTrue();
    }
}

