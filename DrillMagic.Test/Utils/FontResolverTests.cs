using DrillMagic.Windows.Utils;
using FluentAssertions;
using System;
using System.IO;
using Xunit;

namespace DrillMagic.Test.Utils;

public class FontResolverTests
{
    // Use a custom directory for testing if needed, or default
    private readonly FontResolver _sut = new();

    [Fact]
    public void ResolveTypeface_ShouldReturnRegular_WhenNoStyleSpecified()
    {
        // Act
        var result = _sut.ResolveTypeface("Segoe UI", isBold: false, isItalic: false);

        // Assert
        result.FaceName.Should().Be("segoeui.ttf");
    }

    [Fact]
    public void ResolveTypeface_ShouldReturnBold_WhenBoldSpecified()
    {
        // Act
        var result = _sut.ResolveTypeface("Segoe UI", isBold: true, isItalic: false);

        // Assert
        result.FaceName.Should().Be("segoeuib.ttf");
    }

    [Fact]
    public void ResolveTypeface_ShouldReturnItalic_WhenItalicSpecified()
    {
        // Act
        var result = _sut.ResolveTypeface("Segoe UI", isBold: false, isItalic: true);

        // Assert
        result.FaceName.Should().Be("segoeuii.ttf");
    }

    [Fact]
    public void ResolveTypeface_ShouldReturnBoldItalic_WhenBothStylesSpecified()
    {
        // Act
        var result = _sut.ResolveTypeface("Segoe UI", isBold: true, isItalic: true);

        // Assert
        result.FaceName.Should().Be("segoeuiz.ttf");
    }

    [Fact]
    public void ResolveTypeface_ShouldReturnNull_ForUnknownFontFamily()
    {
        // Act
        var result = _sut.ResolveTypeface("Comic Sans", isBold: false, isItalic: false);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetFont_ShouldThrowFileNotFound_WhenFontFileDoesNotExist()
    {
        // Arrange
        var nonExistentFont = "non_existent_font_12345.ttf";

        // Act
        Action act = () => _sut.GetFont(nonExistentFont);

        // Assert
        act.Should().Throw<FileNotFoundException>()
           .WithMessage($"*{nonExistentFont}*");
    }

    [Fact]
    public void GetFont_ShouldReturnData_WhenFontExists()
    {
        // Arrange
        var fontName = "testfont.ttf";
        var assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
        
        // Use the resolver with the custom path
        var sut = new FontResolver(assetsPath);

        // Act
        var result = sut.GetFont(fontName);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }
}