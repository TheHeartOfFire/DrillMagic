

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DrillMagic.Core
{
    public class DrillGrid
    {
        public Color[,] Grid { get; set; } = new Color[0, 0];
        public uint CellSize { get; set; } = 0;
        public float Rotation { get; set; } = 0;
        public float NormalizedStaggerOffset { get; set; } = 0;
        public int Height => Grid.GetLength(0);
        public int Width => Grid.GetLength(1);
        public DrillGrid(int height, int width, 
            uint cellSize = 0, float rotation = 0, 
            float normalizedStaggerOffset = 0)
        {
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
        }

        public DrillGrid(Image<Rgba32> image, 
            int height = 0, int width = 0, 
            uint cellSize = 0, float rotation = 0, 
            float normalizedStaggerOffset = 0)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (height == 0)
                height = image.Height;
            if (width == 0)
                width = image.Width;
            Grid = new Color[height, width];
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    Grid[i, j] = image[i,j];
                }
            }
            CellSize = cellSize;
            Rotation = rotation;
            NormalizedStaggerOffset = normalizedStaggerOffset;
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

    }
}
