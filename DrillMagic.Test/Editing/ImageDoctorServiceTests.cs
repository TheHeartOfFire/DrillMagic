using System.IO;
using DrillMagic.Core;
using DrillMagic.Core.Editing;
using DrillMagic.Core.Types;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DrillMagic.Test.Editing;

public class ImageDoctorServiceTests
{
    private readonly ImageDoctorService _service = new();

    [Fact]
    public void Crop_ReducesDimensionsToSpecifiedBounds()
    {
        using var image = new Image<Rgba32>(100, 100, new Rgba32(255, 0, 0, 255));

        _service.Crop(image, new Rectangle(10, 10, 40, 30));

        Assert.Equal(40, image.Width);
        Assert.Equal(30, image.Height);
    }

    [Fact]
    public void PaintBrush_ColorsPixelAtLocation()
    {
        using var image = new Image<Rgba32>(50, 50, new Rgba32(0, 0, 0, 255));
        var green = new Rgba32(0, 255, 0, 255);

        _service.PaintBrush(image, new Point(25, 25), green, 1f);

        var pixel = image[25, 25];
        Assert.Equal(green, pixel);
    }

    [Fact]
    public void DrawRectangle_Filled_FillsRegion()
    {
        using var image = new Image<Rgba32>(50, 50, new Rgba32(0, 0, 0, 255));
        var yellow = new Rgba32(255, 255, 0, 255);

        _service.DrawRectangle(image, new Rectangle(10, 10, 20, 20), yellow, 1f, filled: true);

        // Center of rectangle
        Assert.Equal(yellow, image[15, 15]);
        // Outside rectangle
        Assert.Equal(new Rgba32(0, 0, 0, 255), image[5, 5]);
    }

    [Fact]
    public void DrawEllipse_Filled_FillsCenter()
    {
        using var image = new Image<Rgba32>(50, 50, new Rgba32(0, 0, 0, 255));
        var blue = new Rgba32(0, 0, 255, 255);

        _service.DrawEllipse(image, new Rectangle(10, 10, 30, 30), blue, 1f, filled: true);

        // Center of ellipse (25, 25)
        Assert.Equal(blue, image[25, 25]);
    }

    [Fact]
    public void DrawLine_ColorsInterveningPixels()
    {
        using var image = new Image<Rgba32>(50, 50, new Rgba32(0, 0, 0, 255));
        var white = new Rgba32(255, 255, 255, 255);

        _service.DrawLine(image, new Point(10, 20), new Point(30, 20), white, 2f);

        // Midpoint of line (20, 20)
        Assert.Equal(white, image[20, 20]);
    }

    [Fact]
    public void CloneImage_CreatesExactIndependentCopy()
    {
        using var image = new Image<Rgba32>(10, 10, new Rgba32(100, 100, 100, 255));

        using var clone = _service.CloneImage(image);

        Assert.Equal(image.Width, clone.Width);
        Assert.Equal(image.Height, clone.Height);

        // Mutating original does not mutate clone
        image[0, 0] = new Rgba32(255, 0, 0, 255);
        Assert.NotEqual(image[0, 0], clone[0, 0]);
    }

    [Fact]
    public void SaveToPngStream_ReturnsValidPngStream()
    {
        using var image = new Image<Rgba32>(20, 20, new Rgba32(50, 50, 50, 255));

        using var stream = _service.SaveToPngStream(image);

        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);

        // Verify stream can be re-loaded
        stream.Position = 0;
        using var reloaded = Image.Load<Rgba32>(stream);
        Assert.Equal(20, reloaded.Width);
        Assert.Equal(20, reloaded.Height);
    }

    [Fact]
    public void DoctoredImage_WhenCroppedAndSaved_ProducesGridWithCroppedDimensions()
    {
        using var image = new Image<Rgba32>(100, 80, new Rgba32(255, 255, 255, 255));
        _service.Crop(image, new Rectangle(10, 10, 40, 30));

        using var stream = _service.SaveToPngStream(image);
        var colorMapServiceMock = new Mock<IColorMapService>();
        colorMapServiceMock
            .Setup(c => c.GetDMCColor(It.IsAny<System.Drawing.Color>()))
            .Returns(new DMCColor("White", 1, "#FFFFFF"));

        var grid = new DrillGrid(colorMapServiceMock.Object, stream, cellSize: 10);

        Assert.Equal(4, grid.Width);  // 40 / 10
        Assert.Equal(3, grid.Height); // 30 / 10
    }

    [Fact]
    public void DoctoredImage_WhenPaintedAndSaved_ProducesGridWithPaintedColor()
    {
        using var image = new Image<Rgba32>(20, 20, new Rgba32(0, 0, 0, 255));
        var red = new Rgba32(255, 0, 0, 255);
        _service.DrawRectangle(image, new Rectangle(0, 0, 20, 20), red, 1f, filled: true);

        using var stream = _service.SaveToPngStream(image);
        var expectedColor = System.Drawing.Color.FromArgb(255, 255, 0, 0);
        var colorMapServiceMock = new Mock<IColorMapService>();
        colorMapServiceMock
            .Setup(c => c.GetDMCColor(It.IsAny<System.Drawing.Color>()))
            .Returns(new DMCColor("Red", 666, "#FF0000"));

        var grid = new DrillGrid(colorMapServiceMock.Object, stream, cellSize: 10);

        Assert.Equal(expectedColor, grid.Grid[0, 0]);
    }
}

