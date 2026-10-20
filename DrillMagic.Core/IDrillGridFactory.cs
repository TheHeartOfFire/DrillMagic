using System.IO;

namespace DrillMagic.Core;

public interface IDrillGridFactory
{
    DrillGrid CreateGrid(int height, int width, uint cellSize = 0, float rotation = 0, float normalizedStaggerOffset = 0);
    DrillGrid CreateGrid(System.Drawing.Color[,] grid, uint cellSize = 0, float rotation = 0, float normalizedStaggerOffset = 0);
    DrillGrid CreateGridFromImage(MemoryStream imageStream, uint cellSize = 0, float rotation = 0, float normalizedStaggerOffset = 0);
    DrillGrid CreateGridFromImage(MemoryStream imageStream, int targetGridWidth, int targetGridHeight, uint cellSize = 0, float rotation = 0, float normalizedStaggerOffset = 0);
}