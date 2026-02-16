using System.Drawing;
using DrillMagic.Core.Types;
using FluentAssertions;
using Xunit;

namespace DrillMagic.Test.Domain;

public class DMCColorTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        string name = "Black";
        uint number = 310;
        string hex = "#000000";

        // Act
        var dmc = new DMCColor(name, number, hex);

        // Assert
        dmc.Name.Should().Be(name);
        dmc.DMCNumber.Should().Be(number);
        dmc.Hex.Should().Be(hex);
    }

    [Fact]
    public void Color_ShouldParseHexCorrectly()
    {
        // Arrange
        var dmc = new DMCColor("Red", 666, "#FF0000");

        // Act & Assert
        dmc.Color.R.Should().Be(255);
        dmc.Color.G.Should().Be(0);
        dmc.Color.B.Should().Be(0);
        dmc.Color.A.Should().Be(255); // Default alpha
    }

    [Fact]
    public void UiColor_ShouldConvertToWindowsUiColor()
    {
        // Arrange
        var dmc = new DMCColor("Green", 100, "#00FF00");

        // Act
        var uiColor = dmc.UiColor;

        // Assert
        uiColor.R.Should().Be(0);
        uiColor.G.Should().Be(255);
        uiColor.B.Should().Be(0);
        uiColor.A.Should().Be(255);
    }

    [Fact]
    public void Empty_ShouldReturnInvalidColor()
    {
        // Act
        var empty = DMCColor.Empty;

        // Assert
        empty.Name.Should().Be("Invalid");
        empty.DMCNumber.Should().Be(uint.MaxValue);
        empty.Hex.Should().Be("#000000");
    }
}
