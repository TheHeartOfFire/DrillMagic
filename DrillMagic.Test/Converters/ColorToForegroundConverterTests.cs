using DrillMagic.Windows.Converters;
using FluentAssertions;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Xunit;

namespace DrillMagic.Test.Converters;

public class ColorToForegroundConverterTests
{
    private readonly ColorToForegroundConverter _sut = new();

    [Fact]
    public void GetContrastColor_WithLightColor_ShouldReturnBlack()
    {
        // Arrange
        var white = Color.FromArgb(255, 255, 255, 255);
        var black = Color.FromArgb(255, 0, 0, 0);

        // Act
        var result = ColorToForegroundConverter.GetContrastColor(white);

        // Assert
        result.Should().Be(black);
    }
}