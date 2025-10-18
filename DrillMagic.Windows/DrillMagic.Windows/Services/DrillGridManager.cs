using CommunityToolkit.Mvvm.ComponentModel;
using DrillMagic.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DrillMagic.Windows.Services;

public partial class DrillGridManager : ObservableObject, IDrillGridManager
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private DrillGrid? _selectedGrid;

    [ObservableProperty]
    private ObservableCollection<uint> _availableCellSizes = [];

    private readonly Dictionary<uint, DrillGrid> _grids = [];
    private string? _imagePath;

    public async Task LoadImageAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            _imagePath = imagePath;
            _grids.Clear();
            AvailableCellSizes.Clear();

            List<uint> sizes = [];
            using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imagePath, cancellationToken))
            {
                var minDimension = Math.Min(image.Width, image.Height);
                for (uint size = 1; size <= 20; size += 1)
                {
                    if (size > minDimension) break;
                    sizes.Add(size);
                }
            }

            foreach (var size in sizes)
            {
                AvailableCellSizes.Add(size);
            }

            if (AvailableCellSizes.Count > 0)
            {
                await SelectGridAsync(AvailableCellSizes[9]);
            }
            else
            {
                SelectedGrid = null;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectGridAsync(uint cellSize)
    {
        if (_grids.TryGetValue(cellSize, out var grid))
        {
            SelectedGrid = grid;
            return;
        }

        if (_imagePath is not null)
        {
            var newGrid = await Task.Run(() => new DrillGrid(_imagePath, cellSize));
            _grids[cellSize] = newGrid;
            SelectedGrid = newGrid;
        }
    }
}
