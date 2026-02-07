using DrillMagic.Core.Types;
using DrillMagic.Windows.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Drawing;

namespace DrillMagic.Test.Services;

public class SharedInteractionServiceTests
{
    private readonly Mock<ILogger<SharedInteractionService>> _loggerMock;
    private readonly SharedInteractionService _sut;

    public SharedInteractionServiceTests()
    {
        _loggerMock = new Mock<ILogger<SharedInteractionService>>();
        _sut = new SharedInteractionService(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeDefaults()
    {
        // Assert
        _sut.CurrentMode.Should().Be(InteractionMode.Navigate);
        _sut.InspectedColor.Name.Should().Be("Invalid");
        _sut.SelectedColor.Name.Should().Be("Black");
    }

    [Fact]
    public void CurrentMode_SetDifferentValue_ShouldUpdateAndRaisePropertyChanged()
    {
        // Arrange
        using var monitor = _sut.Monitor();
        var newMode = InteractionMode.Coloring;

        // Act
        _sut.CurrentMode = newMode;

        // Assert
        _sut.CurrentMode.Should().Be(newMode);
        monitor.Should().RaisePropertyChangeFor(x => x.CurrentMode);
    }

    [Fact]
    public void CurrentMode_SetSameValue_ShouldNotRaisePropertyChanged()
    {
        // Arrange
        var initialMode = _sut.CurrentMode;
        using var monitor = _sut.Monitor();

        // Act
        _sut.CurrentMode = initialMode;

        // Assert
        monitor.Should().NotRaisePropertyChangeFor(x => x.CurrentMode);
    }

    [Fact]
    public void InspectedColor_Set_ShouldUpdateSelectedColorAndRaiseEvents()
    {
        // Arrange
        var newColor = new DMCColor("Red", 666, "#FF0000");
        using var monitor = _sut.Monitor();

        // Act
        _sut.InspectedColor = newColor;

        // Assert
        _sut.InspectedColor.Should().Be(newColor);
        _sut.SelectedColor.ToArgb().Should().Be(ColorTranslator.FromHtml("#FF0000").ToArgb());

        monitor.Should().RaisePropertyChangeFor(x => x.InspectedColor);
        monitor.Should().RaisePropertyChangeFor(x => x.SelectedColor);
    }

    [Fact]
    public void HighlightedColor_Set_ShouldUpdateSelectedColor()
    {
        // Arrange
        var newColor = new DMCColor("Blue", 123, "#0000FF");
        using var monitor = _sut.Monitor();

        // Act
        _sut.HighlightedColor = newColor;

        // Assert
        _sut.HighlightedColor.Should().Be(newColor);
        _sut.SelectedColor.ToArgb().Should().Be(ColorTranslator.FromHtml("#0000FF").ToArgb());
        
        monitor.Should().RaisePropertyChangeFor(x => x.HighlightedColor);
        monitor.Should().RaisePropertyChangeFor(x => x.SelectedColor);
    }
}