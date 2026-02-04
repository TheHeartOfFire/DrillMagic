using System;
using System.Drawing;
using DrillMagic.Core;
using FluentAssertions;
using Xunit;

namespace DrillMagic.Tests.Core;

public class UtilsTests
{
    [Fact]
    public void ChannelWiseAverage_ShouldReturnEmpty_WhenArrayIsNull()
    {
        Color[] colors = null!;
        var result = colors.ChannelWiseAverage();
        result.Should().Be(Color.Empty);
    }

    [Fact]
    public void ChannelWiseAverage_ShouldReturnEmpty_WhenArrayIsEmpty()
    {
        var colors = Array.Empty<Color>();
        var result = colors.ChannelWiseAverage();
        result.Should().Be(Color.Empty);
    }

    [Fact]
    public void ChannelWiseAverage_ShouldReturnAverageColor_Simple()
    {
        var colors = new[]
        {
            Color.FromArgb(0, 0, 0),
            Color.FromArgb(100, 100, 100)
        };
        var result = colors.ChannelWiseAverage();
        result.R.Should().Be(50);
        result.G.Should().Be(50);
        result.B.Should().Be(50);
    }

    [Fact]
    public void ChannelWiseAverage_ShouldReturnAverageColor_Mixed()
    {
        var colors = new[]
        {
            Color.Red,    // 255, 0, 0
            Color.Blue    // 0, 0, 255
        };
        // Expect 127, 0, 127
        var result = colors.ChannelWiseAverage();
        result.R.Should().Be(127);
        result.G.Should().Be(0);
        result.B.Should().Be(127);
    }

    [Fact]
    public void SquaredChannelWiseAverage_ShouldReturnEmpty_WhenArrayIsNull()
    {
        Color[] colors = null!;
        var result = colors.SquaredChannelWiseAverage();
        result.Should().Be(Color.Empty);
    }

    [Fact]
    public void SquaredChannelWiseAverage_ShouldCorrectlyCalculateRMS()
    {
        // Example: sqrt( (10^2 + 30^2)/2 ) = sqrt((100+900)/2) = sqrt(500) = 22.36 -> 22
        var c1 = Color.FromArgb(10, 10, 10);
        var c2 = Color.FromArgb(30, 30, 30);
        var colors = new[] { c1, c2 };
        
        var result = colors.SquaredChannelWiseAverage();
        result.R.Should().Be(22);
    }

    [Fact]
    public void ToUiColor_ShouldConvertCorrectly()
    {
        var drawingColor = Color.FromArgb(255, 10, 20, 30);
        var uiColor = drawingColor.ToUiColor();
        uiColor.A.Should().Be(255);
        uiColor.R.Should().Be(10);
        uiColor.G.Should().Be(20);
        uiColor.B.Should().Be(30);
    }

    [Fact]
    public void ToDrawingColor_ShouldConvertCorrectly()
    {
        var uiColor = Windows.UI.Color.FromArgb(255, 50, 60, 70);
        var drawingColor = uiColor.ToDrawingColor();
        drawingColor.A.Should().Be(255);
        drawingColor.R.Should().Be(50);
        drawingColor.G.Should().Be(60);
        drawingColor.B.Should().Be(70);
    }
    
    [Fact]
    public void HSLAverage_ShouldReturnEmpty_WhenArrayIsNull()
    {
        Color[] colors = null!;
        var result = colors.HSLAverage();
        result.Should().Be(Color.Empty);
    }

    [Fact]
    public void HSLAverage_ShouldReturnEmpty_WhenArrayIsEmpty()
    {
        var colors = Array.Empty<Color>();
        var result = colors.HSLAverage();
        result.Should().Be(Color.Empty);
    }

    [Fact]
    public void HSLAverage_ShouldAverageHueCorrectly_Simple()
    {
        // Red (0 deg) and Green (120 deg) -> Yellow (60 deg) 
        // if fully saturated and same lightness
        var c1 = Color.FromArgb(255, 0, 0); // Red
        var c2 = Color.FromArgb(0, 255, 0); // Green
        
        var result = new[] { c1, c2 }.HSLAverage();
        
        // Result should be approximately Yellow (255, 255, 0)
        // Allowing some tolerance due to float/double conversions
        result.R.Should().BeCloseTo(255, 2);
        result.G.Should().BeCloseTo(255, 2);
        result.B.Should().BeCloseTo(0, 2);
    }

    [Fact]
    public void HSLAverage_ShouldHandleAchromaticColors_ByIgnoringHueWeight()
    {
        // Gray (no saturation) should not pull the hue
        // Red + Gray -> Reddish (but desaturated)
        var red = Color.FromArgb(255, 0, 0);
        var gray = Color.FromArgb(128, 128, 128); // S=0
        
        var result = new[] { red, gray }.HSLAverage();
        
        // Hue should stay Red (0), Saturation halved, Lightness averaged
        // H: 0, S: ~0.5, L: ~0.5
        // Expected approx: R > G=B
        result.R.Should().BeGreaterThan(result.G);
        result.G.Should().Be(result.B); // Should be roughly equal gray
    }
    
    [Fact]
    public void HSLAverage_ShouldWrapHueCorrectly()
    {
        // Red (0/360) and Blue (240). Average on the circle is Magenta (300)
        // (cos(0)+cos(240))/2 = (1 - 0.5)/2 = 0.25
        // (sin(0)+sin(240))/2 = (0 - 0.866)/2 = -0.433
        // atan2(-0.433, 0.25) = -60 deg => 300 deg
        
        var red = Color.FromArgb(255, 0, 0);
        var blue = Color.FromArgb(0, 0, 255);
        
        var result = new[] { red, blue }.HSLAverage();
        
        // Magenta is 255, 0, 255
        result.R.Should().BeCloseTo(255, 2);
        result.G.Should().BeCloseTo(0, 2);
        result.B.Should().BeCloseTo(255, 2);
    }

     [Fact]
    public void FromHSL_ShouldHandleEdgeCases()
    {
        // Black
        var black = Utils.FromHSL(0, 0, 0);
        black.R.Should().Be(0);
        black.G.Should().Be(0);
        black.B.Should().Be(0);

        // White
        var white = Utils.FromHSL(180, 0.5f, 1.0f);
        white.R.Should().Be(255);
        white.G.Should().Be(255);
        white.B.Should().Be(255);

        // Check H prime conversions
        // 0-60 (Red-Yellow) covered by simple tests
        // 60-120 (Yellow-Green)
        var green = Utils.FromHSL(120, 1.0f, 0.5f);
        green.R.Should().Be(0);
        green.G.Should().Be(255);
        green.B.Should().Be(0);
        
        // 180 (Cyan)
        var cyan = Utils.FromHSL(180, 1.0f, 0.5f);
        cyan.R.Should().Be(0);
        cyan.G.Should().Be(255);
        cyan.B.Should().Be(255);
        
        // 240 (Blue)
        var blue = Utils.FromHSL(240, 1.0f, 0.5f);
        blue.R.Should().Be(0);
        blue.G.Should().Be(0);
        blue.B.Should().Be(255);
        
        // 300 (Magenta)
        var magenta = Utils.FromHSL(300, 1.0f, 0.5f);
        magenta.R.Should().Be(255);
        magenta.G.Should().Be(0);
        magenta.B.Should().Be(255);
    }
}
