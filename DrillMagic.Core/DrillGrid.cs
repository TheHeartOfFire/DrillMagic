using DrillMagic.Core.Types;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using Color = System.Drawing.Color;
using System.IO;

namespace DrillMagic.Core
{
    public partial class DrillGrid : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

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
            float normalizedStaggerOffset = 0)
        {
            ArgumentNullException.ThrowIfNull(colorMapService);
            _colorMapService = colorMapService;

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
        }

        public DrillGrid(IColorMapService colorMapService, MemoryStream imageStream,
            uint cellSize = 0, float rotation = 0,
            float normalizedStaggerOffset = 0)
        {
            ArgumentNullException.ThrowIfNull(colorMapService);
            ArgumentNullException.ThrowIfNull(imageStream);
            if (cellSize == 0)
                throw new ArgumentException("Cell size cannot be zero.", nameof(cellSize));

            _colorMapService = colorMapService;

            imageStream.Position = 0;
            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imageStream);

            int gridHeight = image.Height / (int)cellSize;
            int gridWidth = image.Width / (int)cellSize;
            Grid = new Color[gridHeight, gridWidth];

            for (int i = 0; i < gridHeight; i++)
            {
                for (int j = 0; j < gridWidth; j++)
                {
                    var imgColor = ExtractCellColors(image, cellSize, new SixLabors.ImageSharp.Point(j * (int)cellSize, i * (int)cellSize)).SquaredChannelWiseAverage();
                    Color? dmcColor = imgColor != Color.FromArgb(0, 0, 0, 0) ? GetClosestDMCColor(imgColor) : null;
                    if (dmcColor is not null)
                        Grid[i, j] = dmcColor.Value;
                }
            }
            CellSize = cellSize;
            Rotation = rotation;
            NormalizedStaggerOffset = normalizedStaggerOffset;
            RecalculateColorSummary();
        }

        /// <summary>
        /// Reduces the number of colors in the grid to the specified target count.
        /// </summary>
        /// <param name="targetColorCount">The desired number of colors.</param>
        public void ReduceColors(int targetColorCount)
        {
            if (targetColorCount <= 0)
                throw new ArgumentException("Target color count must be greater than zero.", nameof(targetColorCount));

            // Recalculate summary to ensure it's fresh before starting.
            RecalculateColorSummary();
            var currentSummary = new Dictionary<Color, int>(ColorSummary);

            if (currentSummary.Count <= targetColorCount)
                return;

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
