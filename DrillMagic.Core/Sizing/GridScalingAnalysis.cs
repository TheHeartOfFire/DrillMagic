using System;

namespace DrillMagic.Core.Sizing;

/// <summary>
/// Detailed analysis of grid scaling and edge shaving (pixel cropping) when mapping a source image to a target grid.
/// </summary>
public record GridScalingAnalysis
{
    public int ImageWidth { get; init; }
    public int ImageHeight { get; init; }
    public int TargetGridWidth { get; init; }
    public int TargetGridHeight { get; init; }
    public uint EffectiveCellSize { get; init; }
    public int StartOffsetX { get; init; }
    public int StartOffsetY { get; init; }
    public int UsedWidthPixels { get; init; }
    public int UsedHeightPixels { get; init; }
    public int ShavedWidthPixels { get; init; }
    public int ShavedHeightPixels { get; init; }
    public double ShavedWidthPercent { get; init; }
    public double ShavedHeightPercent { get; init; }
    public bool IsExceedingResolution { get; init; }

    public bool HasShaving => ShavedWidthPixels > 0 || ShavedHeightPixels > 0;
    public int ShavedLeftRightEach => ShavedWidthPixels / 2;
    public int ShavedTopBottomEach => ShavedHeightPixels / 2;

    /// <summary>
    /// Analyzes the scaling of an image to target grid dimensions (in drills).
    /// </summary>
    public static GridScalingAnalysis Analyze(int imageWidth, int imageHeight, int targetGridWidth, int targetGridHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || targetGridWidth <= 0 || targetGridHeight <= 0)
        {
            return new GridScalingAnalysis
            {
                ImageWidth = imageWidth,
                ImageHeight = imageHeight,
                TargetGridWidth = targetGridWidth,
                TargetGridHeight = targetGridHeight,
                EffectiveCellSize = 0,
                IsExceedingResolution = true
            };
        }

        if (targetGridWidth > imageWidth || targetGridHeight > imageHeight)
        {
            return new GridScalingAnalysis
            {
                ImageWidth = imageWidth,
                ImageHeight = imageHeight,
                TargetGridWidth = targetGridWidth,
                TargetGridHeight = targetGridHeight,
                EffectiveCellSize = 0,
                IsExceedingResolution = true
            };
        }

        uint cellSizeX = (uint)(imageWidth / targetGridWidth);
        uint cellSizeY = (uint)(imageHeight / targetGridHeight);
        uint effectiveCellSize = Math.Min(cellSizeX, cellSizeY);

        if (effectiveCellSize == 0)
        {
            effectiveCellSize = 1;
        }

        int usedWidth = targetGridWidth * (int)effectiveCellSize;
        int usedHeight = targetGridHeight * (int)effectiveCellSize;

        int shavedWidth = Math.Max(0, imageWidth - usedWidth);
        int shavedHeight = Math.Max(0, imageHeight - usedHeight);

        int startX = shavedWidth / 2;
        int startY = shavedHeight / 2;

        double shavedWPercent = ((double)shavedWidth / imageWidth) * 100.0;
        double shavedHPercent = ((double)shavedHeight / imageHeight) * 100.0;

        return new GridScalingAnalysis
        {
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            TargetGridWidth = targetGridWidth,
            TargetGridHeight = targetGridHeight,
            EffectiveCellSize = effectiveCellSize,
            StartOffsetX = startX,
            StartOffsetY = startY,
            UsedWidthPixels = usedWidth,
            UsedHeightPixels = usedHeight,
            ShavedWidthPixels = shavedWidth,
            ShavedHeightPixels = shavedHeight,
            ShavedWidthPercent = shavedWPercent,
            ShavedHeightPercent = shavedHPercent,
            IsExceedingResolution = false
        };
    }

    /// <summary>
    /// Formats a user-facing explanation of how much of the image is being shaved off the edges.
    /// </summary>
    public string FormatAlertMessage()
    {
        if (IsExceedingResolution)
        {
            return $"Requested {TargetGridWidth}×{TargetGridHeight} drills exceeds source image resolution ({ImageWidth}×{ImageHeight} px). Maximum possible drills is {ImageWidth}×{ImageHeight}.";
        }

        if (!HasShaving)
        {
            return $"Full image represented! 100% of source pixels are covered (Cell size: {EffectiveCellSize} px/drill).";
        }

        if (ShavedWidthPixels > 0 && ShavedHeightPixels == 0)
        {
            return $"Shaving {ShavedWidthPixels} px horizontally ({ShavedWidthPercent:F1}%) — {ShavedLeftRightEach} px cropped from left & right edges. Full height represented. Cell size: {EffectiveCellSize} px/drill.";
        }

        if (ShavedWidthPixels == 0 && ShavedHeightPixels > 0)
        {
            return $"Shaving {ShavedHeightPixels} px vertically ({ShavedHeightPercent:F1}%) — {ShavedTopBottomEach} px cropped from top & bottom edges. Full width represented. Cell size: {EffectiveCellSize} px/drill.";
        }

        return $"Shaving {ShavedWidthPixels} px horizontally ({ShavedWidthPercent:F1}%) and {ShavedHeightPixels} px vertically ({ShavedHeightPercent:F1}%) — {ShavedLeftRightEach} px left/right, {ShavedTopBottomEach} px top/bottom. Cell size: {EffectiveCellSize} px/drill.";
    }
}

