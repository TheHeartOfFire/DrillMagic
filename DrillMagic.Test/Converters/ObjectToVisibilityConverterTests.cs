using DrillMagic.Windows.Converters;
using FluentAssertions;
using Microsoft.UI.Xaml;
using Xunit;

namespace DrillMagic.Test.Converters;

public class ObjectToVisibilityConverterTests
{
    private readonly ObjectToVisibilityConverter _sut = new ObjectToVisibilityConverter();

    [Fact]
    public void Convert_ShouldReturnVisible_WhenValueIsNotNull()
    {
        // Act
        var result = _sut.Convert(new object(), null, null, null);

        // Assert
        result.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void Convert_ShouldReturnCollapsed_WhenValueIsNull()
    {
        // Act
        var result = _sut.Convert(null, null, null, null);

        // Assert
        result.Should().Be(Visibility.Collapsed);
    }
}
