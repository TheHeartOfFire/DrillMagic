using DrillMagic.Core.Types;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using Color = System.Drawing.Color;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DrillMagic.Core
{
    public partial class DrillGrid : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly ILogger _logger;

        public Color[,] Grid { get; set; } = new Color[0, 0];
        public uint CellSize { get; set; } = 0;
        public float Rotation { get; set; } = 0;
        public float NormalizedStaggerOffset { get; set; } = 0;
        public int Height => Grid.GetLength(0);
        public int Width => Grid.GetLength(1);

        private Dictionary<Color, int> _colorSummary = [];
        public Dictionary<Color, int> ColorSummary
        {
            get => _colorSummary;
            private set
            {
                _colorSummary = value;
                OnPropertyChanged();
            }
        }

        private readonly Dictionary<Color, Color> _buffer = [];
        private readonly IColorMapService _colorMapService;

        public DrillGrid(IColorMapService colorMapService, int height, int width,
            uint cellSize = 0, float rotation = 0,
            float normalizedStaggerOffset = 0, ILogger<DrillGrid>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(colorMapService);
            _colorMapService = colorMapService;
            _logger = logger ?? NullLogger<DrillGrid>.Instance;

            Grid = new Color[height, width];
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    Grid[i, j] = Color.Transparent;
                }
            }
            CellSize = cellSize;
            Rotation = rotation;
            NormalizedStaggerOffset = normalizedStaggerOffset;
            RecalculateColorSummary();
            _logger.LogInformation("DrillGrid initialized (Empty) with size {Width}x{Height}", width, height);
        }

        public DrillGrid(IColorMapService colorMapService, Color[,] grid,
            uint cellSize = 0, float rotation = 0,
            float normalizedStaggerOffset = 0, ILogger<DrillGrid>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(colorMapService);
            ArgumentNullException.ThrowIfNull(grid);
            _colorMapService = colorMapService;
            _logger = logger ?? NullLogger<DrillGrid>.Instance;

            Grid = grid;
            CellSize = cellSize;
            Rotation = rotation;
            NormalizedStaggerOffset = normalizedStaggerOffset;
            RecalculateColorSummary();
            _logger.LogInformation("DrillGrid initialized from matrix with size {Width}x{Height}", Width, Height);
        }

        public DrillGrid(IColorMapService colorMapService, MemoryStream imageStream,
            uint cellSize = 0, float rotation = 0,
            float normalizedStaggerOffset = 0, ILogger<DrillGrid>? logger = null)
            : this(colorMapService, imageStream, targetGridWidth: 0, targetGridHeight: 0, cellSize: cellSize,
                   startOffsetX: -1, startOffsetY: -1, rotation: rotation, normalizedStaggerOffset: normalizedStaggerOffset, logger: logger)
        {
        }

        public DrillGrid(IColorMapService colorMapService, MemoryStream imageStream,
            int targetGridWidth, int targetGridHeight,
            uint cellSize = 0, int startOffsetX = -1, int startOffsetY = -1,
            float rotation = 0, float normalizedStaggerOffset = 0, ILogger<DrillGrid>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(colorMapService);
            ArgumentNullException.ThrowIfNull(imageStream);

            _colorMapService = colorMapService;
            _logger = logger ?? NullLogger<DrillGrid>.Instance;

            imageStream.Position = 0;
            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageStream);
            _logger.LogDebug("Image loaded. Dimensions: {Width}x{Height}", image.Width, image.Height);

            int gridWidth;
            int gridHeight;

            if (targetGridWidth > 0 && targetGridHeight > 0)
            {
                gridWidth = targetGridWidth;
                gridHeight = targetGridHeight;

                if (cellSize == 0)
                {
                    uint cX = (uint)(image.Width / targetGridWidth);
                    uint cY = (uint)(image.Height / targetGridHeight);
                    cellSize = Math.Max(1, Math.Min(cX, cY));
                }
            }
            else
            {
                if (cellSize == 0)
                    throw new ArgumentException("Cell size cannot be zero when target dimensions are not provided.", nameof(cellSize));

                gridWidth = image.Width / (int)cellSize;
                gridHeight = image.Height / (int)cellSize;
            }

            if (startOffsetX < 0)
            {
                startOffsetX = Math.Max(0, (image.Width - (gridWidth * (int)cellSize)) / 2);
            }
            if (startOffsetY < 0)
            {
                startOffsetY = Math.Max(0, (image.Height - (gridHeight * (int)cellSize)) / 2);
            }

            _logger.LogInformation("Initializing DrillGrid from image stream with dimensions {Width}x{Height}, cell size {CellSize}, offset ({OffsetX}, {OffsetY})",
                gridWidth, gridHeight, cellSize, startOffsetX, startOffsetY);

            Grid = new Color[gridHeight, gridWidth];

            for (int i = 0; i < gridHeight; i++)
            {
                for (int j = 0; j < gridWidth; j++)
                {
                    int px = startOffsetX + (j * (int)cellSize);
                    int py = startOffsetY + (i * (int)cellSize);
                    var imgColor = ExtractCellColors(image, cellSize, new SixLabors.ImageSharp.Point(px, py)).SquaredChannelWiseAverage();
                    Color? dmcColor = imgColor != Color.FromArgb(0, 0, 0, 0) ? GetClosestDMCColor(imgColor) : null;
                    if (dmcColor is not null)
                        Grid[i, j] = dmcColor.Value;
                }
            }
            CellSize = cellSize;
            Rotation = rotation;
            NormalizedStaggerOffset = normalizedStaggerOffset;
            RecalculateColorSummary();
            _logger.LogInformation("DrillGrid generation complete. Total unique colors: {ColorCount}", ColorSummary.Count);
        }

        /// <summary>
        /// Reduces the number of colors in the grid to the specified target count.
        /// </summary>
        /// <param name="targetColorCount">The desired number of colors.</param>
        public void ReduceColors(int targetColorCount)
        {
            if (targetColorCount <= 0)
                throw new ArgumentException("Target color count must be greater than zero.", nameof(targetColorCount));

            _logger.LogInformation("Starting color reduction. Target: {TargetCount}, Current: {CurrentCount}", targetColorCount, ColorSummary.Count);

            // Recalculate summary to ensure it's fresh before starting.
            RecalculateColorSummary();
            var currentSummary = new Dictionary<Color, int>(ColorSummary);

            if (currentSummary.Count <= targetColorCount)
            {
                _logger.LogInformation("Current color count is already less than or equal to target. No reduction needed.");
                return;
            }

            while (currentSummary.Count > targetColorCount)
            {
                var uniqueColors = currentSummary.Keys.ToList();
                if (uniqueColors.Count <= targetColorCount) break;

                float minDistance = float.MaxValue;
                Color color1 = Color.Empty, color2 = Color.Empty;

                // Find the two closest colors in the current palette
                for (int i = 0; i < uniqueColors.Count; i++)
                {
                    for (int j = i + 1; j < uniqueColors.Count; j++)
                    {
                        float distance = GetEuclideanDistance(uniqueColors[i], uniqueColors[j]);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            color1 = uniqueColors[i];
                            color2 = uniqueColors[j];
                        }
                    }
                }

                // Determine which color has fewer cells
                Color colorToKeep = currentSummary[color1] >= currentSummary[color2] ? color1 : color2;
                Color colorToReplace = colorToKeep == color1 ? color2 : color1;

                // Replace the less frequent color with the more frequent color
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        if (Grid[y, x] == colorToReplace)
                        {
                            Grid[y, x] = colorToKeep;
                        }
                    }
                }

                // Update the summary for the next iteration
                currentSummary[colorToKeep] += currentSummary[colorToReplace];
                currentSummary.Remove(colorToReplace);
            }

            // Final recalculation to update the public property and notify listeners
            RecalculateColorSummary();
            _logger.LogInformation("Color reduction complete. Final color count: {FinalCount}", ColorSummary.Count);
        }

        /// <summary>
        /// Returns how many colors in the current grid have fewer instances than the specified minimum count threshold.
        /// </summary>
        public int GetColorsBelowThresholdCount(int minCount)
        {
            if (minCount <= 1 || ColorSummary == null) return 0;
            return ColorSummary.Values.Count(c => c < minCount);
        }

        /// <summary>
        /// Eliminates rare / confetti colors whose frequency is below <paramref name="minCount"/> by merging them
        /// into the closest color in the current palette (by Euclidean RGB distance).
        /// Guarantees that at least one color remains if all colors fall below the threshold.
        /// </summary>
        /// <param name="minCount">The minimum number of drill instances required for a color to be kept.</param>
        /// <returns>The number of unique colors eliminated and merged.</returns>
        public int EnforceMinimumColorCount(int minCount)
        {
            if (minCount <= 1)
                return 0;

            _logger.LogInformation("Enforcing minimum color count threshold: {MinCount}. Initial unique colors: {Count}", minCount, ColorSummary.Count);

            RecalculateColorSummary();
            var currentSummary = new Dictionary<Color, int>(ColorSummary);

            if (currentSummary.Count <= 1)
            {
                return 0;
            }

            int initialCount = currentSummary.Count;
            var replacementMap = new Dictionary<Color, Color>();

            while (currentSummary.Count > 1)
            {
                // Find colors below threshold
                var subThreshold = currentSummary.Where(kvp => kvp.Value < minCount).ToList();
                if (subThreshold.Count == 0)
                {
                    break;
                }

                // Pick the rarest color (smallest count)
                var colorToReplace = subThreshold.OrderBy(kvp => kvp.Value).First().Key;

                // Find closest candidate color in currentSummary excluding colorToReplace
                float minDistance = float.MaxValue;
                Color bestCandidate = Color.Empty;

                foreach (var candidate in currentSummary.Keys)
                {
                    if (candidate == colorToReplace) continue;

                    float distance = GetEuclideanDistance(colorToReplace, candidate);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestCandidate = candidate;
                    }
                }

                if (bestCandidate == Color.Empty)
                {
                    break;
                }

                // Update existing mappings pointing to colorToReplace
                foreach (var key in replacementMap.Keys.ToList())
                {
                    if (replacementMap[key] == colorToReplace)
                    {
                        replacementMap[key] = bestCandidate;
                    }
                }
                replacementMap[colorToReplace] = bestCandidate;

                // Update summary
                currentSummary[bestCandidate] += currentSummary[colorToReplace];
                currentSummary.Remove(colorToReplace);
            }

            if (replacementMap.Count > 0)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        var original = Grid[y, x];
                        if (replacementMap.TryGetValue(original, out var replaced))
                        {
                            Grid[y, x] = replaced;
                        }
                    }
                }

                RecalculateColorSummary();
            }

            int eliminatedCount = initialCount - ColorSummary.Count;
            _logger.LogInformation("Minimum color count enforcement complete. Eliminated: {Eliminated}, Remaining: {Remaining}", eliminatedCount, ColorSummary.Count);
            return eliminatedCount;
        }

        public void RecalculateColorSummary()
        {
            var newSummary = new Dictionary<Color, int>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var color = Grid[y, x];
                    if (color.A > 0) // Exclude transparent
                    {
                        newSummary.TryGetValue(color, out int count);
                        newSummary[color] = count + 1;
                    }
                }
            }
            ColorSummary = newSummary;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Color GetClosestDMCColor(Color color)
        {
            if (_buffer.TryGetValue(color, out Color value))
                return value;

            var dmcColor = _colorMapService.FindClosestDMCColor(color);

            // If for some reason no closest color was found (e.g. empty map), fall back to the original color
            // Check for MaxValue (or "Invalid") as DMCColor.Empty uses it
            Color result = (dmcColor != null && dmcColor.DMCNumber != uint.MaxValue) ? dmcColor.Color : color;

            _buffer[color] = result;
            return result;
        }

        /// <summary>
        /// Returns the Euclidean distance between two colors
        /// SQRT((R1-R2)^2 + (G1-G2)^2 + (B1-B2)^2)
        /// </summary>
        /// <param name="color1"></param>
        /// <param name="color2"></param>
        /// <returns>Euclidean distance between color1 and color2</returns>
        private static float GetEuclideanDistance(Color color1, Color color2)
        {
            // Calculate the difference between the two colors
            var deltaR = color1.R - color2.R;
            var deltaG = color1.G - color2.G;
            var deltaB = color1.B - color2.B;

            // Calculate the square of the differences
            var sqR = deltaR * deltaR;
            var sqG = deltaG * deltaG;
            var sqB = deltaB * deltaB;

            // Calculate the sum of the squares
            var sqSum = sqR + sqG + sqB;

            // Calculate the square root of the sum of the squares
            // and return the Euclidean distance
            return (float)Math.Sqrt(sqSum);
        }

        // Updated to use ImageSharp's Image<Rgba32> and Point types.
        private static Color[] ExtractCellColors(Image<Rgba32> image, uint cellSize = 0, SixLabors.ImageSharp.Point cell = new())
        {
            var colors = new List<Color>();
            for (int i = 0; i < (int)cellSize; i++)
            {
                for (int j = 0; j < (int)cellSize; j++)
                {
                    int px = cell.X + j;
                    int py = cell.Y + i;
                    if (px < 0 || py < 0 || px >= image.Width || py >= image.Height)
                        continue;

                    Rgba32 pixel = image[px, py];
                    if (pixel.A != 0)
                    {
                        // Convert from ImageSharp's Rgba32 to System.Drawing.Color
                        colors.Add(Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B));
                    }
                }
            }
            return colors.ToArray();
        }
    }
}
