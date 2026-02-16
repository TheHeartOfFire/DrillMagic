using System.Drawing;
using DrillMagic.Windows.Converters;
using FluentAssertions;
using Microsoft.UI;
using Xunit;
using WinUIColor = Windows.UI.Color;

namespace DrillMagic.Test.Converters;

public class DrawingToUiColorConverterTests
{
    private readonly DrawingToUiColorConverter _sut = new();

    [Fact]
    public void Convert_ShouldReturnUiColor_WhenValueIsDrawingColor()
    {
        // Arrange
        var drawingColor = Color.FromArgb(255, 128, 64, 32);
        
        // Act
        var result = _sut.Convert(drawingColor, null, null, null);

        // Assert
        result.Should().BeOfType<WinUIColor>();
        var uiColor = (WinUIColor)result;
        uiColor.A.Should().Be(255);
        uiColor.R.Should().Be(128);
        uiColor.G.Should().Be(64);
        uiColor.B.Should().Be(32);
    }

    [Fact]
    public void Convert_ShouldReturnTransparent_WhenValueIsNotDrawingColor()
    {
        // Act
        var result = _sut.Convert("not a color", null, null, null);

        // Assert
        // Expect ARGB(0,0,0,0) instead of using the static property which may fail initialization
        result.Should().Be(WinUIColor.FromArgb(0, 0, 0, 0));
    }

      [Fact]
    public void Convert_ShouldReturnTransparent_WhenValueIsNull()
    {
        // Act
        var result = _sut.Convert(null, null, null, null);

        // Assert
        result.Should().Be(WinUIColor.FromArgb(0, 0, 0, 0));
    }
}
