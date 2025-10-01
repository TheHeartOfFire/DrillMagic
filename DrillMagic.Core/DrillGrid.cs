using DrillMagic.Core.Types;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Color = System.Drawing.Color;
// Note: System.Drawing.Common is still used for Color, but not for image processing.
// This maintains compatibility with the UI project.

namespace DrillMagic.Core
{
    public class DrillGrid : INotifyPropertyChanged
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

        private Dictionary<Color, Color> _buffer = [];

        public DrillGrid(int height, int width,
            uint cellSize = 0, float rotation = 0,
            float normalizedStaggerOffset = 0)
        {
            if (ColorMap.DefaultColorMap.Count == 0) ColorMap.Initialize();
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

        // Replaced Bitmap with a file path and uses ImageSharp for processing.
        public DrillGrid(string imagePath,
            uint cellSize = 0, float rotation = 0,
            float normalizedStaggerOffset = 0)
        {
            ArgumentNullException.ThrowIfNull(imagePath);
            if (cellSize == 0)
                throw new ArgumentException("Cell size cannot be zero.", nameof(cellSize));
            if (ColorMap.DefaultColorMap.Count == 0) ColorMap.Initialize();

            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(imagePath);

            int gridHeight = image.Height / (int)cellSize;
            int gridWidth = image.Width / (int)cellSize;
            Grid = new Color[gridHeight, gridWidth];

            for (int i = 0; i < gridHeight; i++)
            {
                for (int j = 0; j < gridWidth; j++)
                {
                    var imgColor = ExtractCellColors(image, cellSize, new SixLabors.ImageSharp.Point(j * (int)cellSize, i * (int)cellSize)).SquaredChannelWiseAverage();
                    Color? dmcColor = imgColor != Color.FromArgb(0, 0, 0, 0) ? GetClosestDMCColor(imgColor, _buffer) : null;
                    if (dmcColor is not null)
                        Grid[i, j] = dmcColor.Value;
                }
            }
            CellSize = cellSize;
            Rotation = rotation;
            NormalizedStaggerOffset = normalizedStaggerOffset;
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

        public static Color GetClosestDMCColor(Color color, Dictionary<Color, Color> buffer)
        {
            if (buffer.TryGetValue(color, out Color value))
                return value;
            // Initialize the closest color and the minimum distance
            Color closestColor = Color.Empty;
            float minDistance = float.MaxValue;
            // Iterate through the list of DMC colors
            foreach (var dmcColor in Types.ColorMap.DefaultColorMap.Values)
            {
                // Calculate the Euclidean distance between the input color and the DMC color
                float distance = GetEuclideanDistance(color, ColorTranslator.FromHtml(dmcColor.Hex));
                // If the distance is smaller than the minimum distance, update the closest color and the minimum distance
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestColor = ColorTranslator.FromHtml(dmcColor.Hex);
                }
            }
            buffer[color] = closestColor;
            // Return the closest DMC color
            return closestColor;
        }

        /// <summary>
        /// returns the euclidean distance between two colors
        /// SQRT((R1-R2)^2 + (G1-G2)^2 + (B1-B2)^2)
        /// </summary>
        /// <param name="color1"></param>
        /// <param name="color2"></param>
        /// <returns>euclidean distance between color1 and color2</returns>
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
            for (int i = 0; i < cellSize; i++)
            {
                for (int j = 0; j < cellSize; j++)
                {
                    Rgba32 pixel = image[cell.X + j, cell.Y + i];
                    if (pixel.A != 0)
                    {
                        // Convert from ImageSharp's Rgba32 to System.Drawing.Color
                        colors.Add(Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B));
                    }
                }
            }
            return [.. colors];
        }
    }
}
