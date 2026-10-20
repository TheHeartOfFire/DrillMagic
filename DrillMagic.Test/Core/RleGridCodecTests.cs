using DrillMagic.Core.Projects;
using DrillMagic.Core.Types;
using FluentAssertions;
using Moq;
using System;
using System.Drawing;
using System.IO;
using Xunit;

namespace DrillMagic.Test.Core;

public class RleGridCodecTests
{
    private readonly Mock<IColorMapService> _colorMapServiceMock;

    public RleGridCodecTests()
    {
        _colorMapServiceMock = new Mock<IColorMapService>();
        _colorMapServiceMock.Setup(s => s.GetDMCColor(It.IsAny<Color>()))
            .Returns((Color c) => new DMCColor("Color_" + c.Name, 310, ColorTranslator.ToHtml(c)));
    }

    [Fact]
    public void Encode_EmptyGrid_ReturnsEmptyLists()
    {
        var grid = new Color[0, 0];
        var (palette, cells) = RleGridCodec.Encode(grid, _colorMapServiceMock.Object);

        palette.Should().BeEmpty();
        cells.Should().BeEmpty();
    }

    [Fact]
    public void Encode_And_Decode_UniformGrid_RoundtripsSuccessfully()
    {
        var original = new Color[3, 4];
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                original[y, x] = Color.FromArgb(255, 10, 20, 30);
            }
        }

        var (palette, cells) = RleGridCodec.Encode(original, _colorMapServiceMock.Object);

        palette.Should().HaveCount(1);
        cells.Should().HaveCount(1);
        cells[0][0].Should().Be(0); // palette index 0
        cells[0][1].Should().Be(12); // 3 * 4 = 12 cells

        var decoded = RleGridCodec.Decode(4, 3, palette, cells);
        decoded.GetLength(0).Should().Be(3);
        decoded.GetLength(1).Should().Be(4);

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                decoded[y, x].ToArgb().Should().Be(original[y, x].ToArgb());
            }
        }
    }

    [Fact]
    public void Encode_And_Decode_CheckerboardPattern_RoundtripsAccurately()
    {
        int height = 4;
        int width = 5;
        var original = new Color[height, width];
        var c1 = Color.FromArgb(255, 255, 0, 0);
        var c2 = Color.FromArgb(255, 0, 0, 255);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                original[y, x] = (x + y) % 2 == 0 ? c1 : c2;
            }
        }

        var (palette, cells) = RleGridCodec.Encode(original, _colorMapServiceMock.Object);
        palette.Should().HaveCount(2);

        var decoded = RleGridCodec.Decode(width, height, palette, cells);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                decoded[y, x].ToArgb().Should().Be(original[y, x].ToArgb());
            }
        }
    }

    [Fact]
    public void Decode_InvalidCellCount_ThrowsInvalidDataException()
    {
        var palette = new List<PaletteEntry>
        {
            new() { PaletteIndex = 0, Argb = "#FFFF0000" }
        };
        var cells = new List<int[]>
        {
            new[] { 0, 5 } // only 5 cells provided for a 3x3 (9 cells) grid
        };

        var act = () => RleGridCodec.Decode(3, 3, palette, cells);
        act.Should().Throw<InvalidDataException>();
    }
}

