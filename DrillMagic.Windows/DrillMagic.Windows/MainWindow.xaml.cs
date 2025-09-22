using DrillMagic.Core;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;

namespace DrillMagic.Windows;

public sealed partial class MainWindow : WinUIEx.WindowEx
{
    public MainWindow()
    {
        this.InitializeComponent();
        Title = "Drill Magic";

        // Define the path to the image
        string imagePath = @"C:\Users\heart\source\repos\TheHeartOfFire\DrillMagic.Core\DrillMagic.Utils\Resources\AlbumArt1.jpeg";

        if (File.Exists(imagePath))
        {
            // Create the DrillGrid from the image path with a specified cell size.
            uint cellSize = 8;
            var grid = new DrillGrid(imagePath, cellSize: cellSize);

            // Assign the generated grid to the view
            MyDrillGridView.Grid = grid;
        }
        else
        {
            // Fallback for when the image is not found: create a random grid.
            var grid = new DrillGrid(200, 200, 1);
            var random = new Random();
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (random.Next(0, 5) == 0)
                    {
                        grid.Grid[y, x] = Color.FromArgb(128, random.Next(0, 256), random.Next(0, 256), random.Next(0, 256));
                    }
                }
            }
            MyDrillGridView.Grid = grid;
        }
    }
}