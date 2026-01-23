using DrillMagic.Core.Types;
using System;
using System.IO;

namespace DrillMagic.Core;

public class DrillGridFactory : IDrillGridFactory
{
    private readonly IColorMapService _colorMapService;

    public DrillGridFactory(IColorMapService colorMapService)
    {
        _colorMapService = colorMapService ?? throw new ArgumentNullException(nameof(colorMapService));
    }

    public DrillGrid CreateGrid(int height, int width, uint cellSize = 0, float rotation = 0, float normalizedStaggerOffset = 0)
    {
        return new DrillGrid(_colorMapService, height, width, cellSize, rotation, normalizedStaggerOffset);
    }

    public DrillGrid CreateGridFromImage(MemoryStream imageStream, uint cellSize = 0, float rotation = 0, float normalizedStaggerOffset = 0)
    {
        return new DrillGrid(_colorMapService, imageStream, cellSize, rotation, normalizedStaggerOffset);
    }
}