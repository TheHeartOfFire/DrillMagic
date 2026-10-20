using System;

namespace DrillMagic.Core.Sizing;

/// <summary>
/// Calculates real-world physical canvas dimensions for diamond paintings based on drill count and drill shape.
/// </summary>
public static class PhysicalDimensionsCalculator
{
    /// <summary>
    /// Diameter/width of a standard square diamond drill in millimeters.
    /// </summary>
    public const double SquareDrillMm = 2.5;

    /// <summary>
    /// Diameter/width of a standard round diamond drill in millimeters.
    /// </summary>
    public const double RoundDrillMm = 2.8;

    /// <summary>
    /// Calculates the physical dimensions in centimeters and inches.
    /// </summary>
    /// <param name="gridWidth">Width in drills.</param>
    /// <param name="gridHeight">Height in drills.</param>
    /// <param name="shape">Drill shape (Square = 2.5mm, Round = 2.8mm).</param>
    /// <returns>Width and Height in centimeters and inches.</returns>
    public static (double WidthCm, double HeightCm, double WidthInches, double HeightInches) CalculateDimensions(
        int gridWidth, int gridHeight, DrillShape shape)
    {
        if (gridWidth <= 0 || gridHeight <= 0)
        {
            return (0, 0, 0, 0);
        }

        double drillMm = shape == DrillShape.Square ? SquareDrillMm : RoundDrillMm;

        double widthCm = (gridWidth * drillMm) / 10.0;
        double heightCm = (gridHeight * drillMm) / 10.0;

        double widthInches = (gridWidth * drillMm) / 25.4;
        double heightInches = (gridHeight * drillMm) / 25.4;

        return (widthCm, heightCm, widthInches, heightInches);
    }

    /// <summary>
    /// Formats the physical dimensions into a concise display string (e.g. "40.0 × 50.0 cm (15.7″ × 19.7″)").
    /// </summary>
    public static string FormatDimensions(int gridWidth, int gridHeight, DrillShape shape)
    {
        if (gridWidth <= 0 || gridHeight <= 0)
        {
            return "0.0 × 0.0 cm";
        }

        var (wCm, hCm, wIn, hIn) = CalculateDimensions(gridWidth, gridHeight, shape);
        return $"{wCm:F1} × {hCm:F1} cm ({wIn:F1}″ × {hIn:F1}″)";
    }

    /// <summary>
    /// Calculates projected grid dimensions from an image size and cell size.
    /// </summary>
    public static (int ProjectedWidth, int ProjectedHeight) CalculateProjectedGridSize(
        int imageWidth, int imageHeight, uint cellSize)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || cellSize == 0)
        {
            return (0, 0);
        }

        int w = (int)Math.Ceiling((double)imageWidth / cellSize);
        int h = (int)Math.Ceiling((double)imageHeight / cellSize);
        return (w, h);
    }
}

