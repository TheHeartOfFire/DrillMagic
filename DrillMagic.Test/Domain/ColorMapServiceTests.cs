using FluentAssertions;
using Xunit;
using Moq;
using DrillMagic.Core.Types;
using System.Drawing;
using System.Collections.Generic;
using System;

namespace DrillMagic.Test.Domain;

public class ColorMapServiceTests
{
    private readonly Mock<IColorMapRepository> _mockRepository;
    private readonly ColorMapService _service;
    private readonly Dictionary<uint, DMCColor> _testColorMap;

    public ColorMapServiceTests()
    {
        _mockRepository = new Mock<IColorMapRepository>();
        
        // Setup default data
        _testColorMap = new Dictionary<uint, DMCColor>
        {
            { 310, new DMCColor("Black", 310, "#000000") },
            { 5200, new DMCColor("White", 5200, "#FFFFFF") },
            { 666, new DMCColor("Red", 666, "#FF0000") }
        };

        _mockRepository.Setup(r => r.DefaultColorMap).Returns(_testColorMap);
        _mockRepository.Setup(r => r.CustomMappings).Returns(new List<Dictionary<uint, string>>());

        _service = new ColorMapService(_mockRepository.Object);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenRepositoryIsNull()
    {
        Action act = () => new ColorMapService(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        _service.DefaultColorMap.Should().BeEquivalentTo(_testColorMap);
        _service.CustomMappings.Should().NotBeNull();
    }

    [Fact]
    public void SaveCustomMappings_ShouldCallRepositorySave()
    {
        _service.SaveCustomMappings();
        _mockRepository.Verify(r => r.SaveCustomMappings(), Times.Once);
    }

    [Fact]
    public void GetDMCColor_ByNumber_ShouldReturnColor_WhenExists()
    {
        var result = _service.GetDMCColor(310);
        result.Should().NotBeNull();
        result.DMCNumber.Should().Be(310);
        result.Name.Should().Be("Black");
    }

    [Fact]
    public void GetDMCColor_ByNumber_ShouldReturnEmpty_WhenNotExists()
    {
        var result = _service.GetDMCColor(9999);
        result.Should().BeEquivalentTo(DMCColor.Empty);
    }

    [Fact]
    public void GetDMCColor_ByName_ShouldReturnColor_WhenExists()
    {
        var result = _service.GetDMCColor("Black");
        result.Should().NotBeNull();
        result.DMCNumber.Should().Be(310);
        
        // Case insensitive check
        var resultCase = _service.GetDMCColor("black");
        resultCase.DMCNumber.Should().Be(310);
    }

    [Fact]
    public void GetDMCColor_ByName_ShouldReturnEmpty_WhenNotExists()
    {
        var result = _service.GetDMCColor("NonExistent");
        result.Should().BeEquivalentTo(DMCColor.Empty);
    }

    [Fact]
    public void GetDMCColor_ByColor_ShouldReturnColor_WhenExactMatch()
    {
        var blackColor = ColorTranslator.FromHtml("#000000");
        var result = _service.GetDMCColor(blackColor);
        result.DMCNumber.Should().Be(310);
    }

    [Fact]
    public void GetDMCColor_ByColor_ShouldReturnEmpty_WhenNoExactMatch()
    {
        var grayColor = Color.FromArgb(128, 128, 128);
        var result = _service.GetDMCColor(grayColor);
        result.Should().BeEquivalentTo(DMCColor.Empty);
    }

    [Fact]
    public void FindClosestDMCColor_ShouldReturnClosestMatch()
    {
        // Dark gray close to black
        var darkGray = Color.FromArgb(10, 10, 10);
        var result = _service.FindClosestDMCColor(darkGray);
        result.DMCNumber.Should().Be(310); // Black

        // Pinkish red close to Red
        var pinkish = Color.FromArgb(250, 50, 50);
        var result2 = _service.FindClosestDMCColor(pinkish);
        result2.DMCNumber.Should().Be(666); // Red
    }

    [Fact]
    public void FindClosestDMCColor_ShouldReturnEmpty_WhenMapIsEmpty()
    {
        _mockRepository.Setup(r => r.DefaultColorMap).Returns(new Dictionary<uint, DMCColor>());
        // Have to recreate service to get new map property
        var service = new ColorMapService(_mockRepository.Object);
        
        var result = service.FindClosestDMCColor(Color.Black);
        result.Should().BeEquivalentTo(DMCColor.Empty);
    }
    
    [Fact]
    public void GetDMCColor_ByUiColor_ShouldReturnColor_WhenExactMatch()
    {
        var blackColor = global::Windows.UI.Color.FromArgb(255, 0, 0, 0); // ARGB
        var result = _service.GetDMCColor(blackColor);
        result.DMCNumber.Should().Be(310);
    }
}
