using CommunityToolkit.Mvvm.ComponentModel;
using DrillMagic.Core;
using DrillMagic.Core.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace DrillMagic.Windows.Services;

public partial class DrillGridManager(IColorMapService colorMapService, IServiceProvider serviceProvider, ILogger<DrillGridManager> logger) : ObservableObject, IDrillGridManager
{
    private readonly ILogger<DrillGridManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private DrillGrid? _selectedGrid;

    [ObservableProperty]
    private ObservableCollection<uint> _availableCellSizes = [];

    private readonly Dictionary<uint, DrillGrid> _grids = [];
    private MemoryStream? _imageStream;
    private readonly IColorMapService _colorMapService = colorMapService ?? throw new ArgumentNullException(nameof(colorMapService));

    public async Task LoadImageAsync(MemoryStream imageStream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting LoadImageAsync");
        IsBusy = true;
        try
        {
            _imageStream = imageStream;
            _grids.Clear();
            AvailableCellSizes.Clear();

            List<uint> sizes = [];
            _logger.LogInformation("Loading image to determine dimensions");
            using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imageStream, cancellationToken))
            {
                var minDimension = Math.Min(image.Width, image.Height);
                _logger.LogInformation("Image loaded. Dimensions: {Width}x{Height}", image.Width, image.Height);

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
                // Safety check for index 9
                var defaultIndex = Math.Min(9, AvailableCellSizes.Count - 1);
                var sizeToSelect = AvailableCellSizes[defaultIndex];
                _logger.LogInformation("Selecting default grid size: {Size} (Index: {Index})", sizeToSelect, defaultIndex);
                await SelectGridAsync(sizeToSelect);
            }
            else
            {
                _logger.LogWarning("No suitable cell sizes found.");
                SelectedGrid = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LoadImageAsync");
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectGridAsync(uint cellSize)
    {
        _logger.LogInformation("SelectGridAsync called for size: {CellSize}", cellSize);
        if (_grids.TryGetValue(cellSize, out var grid))
        {
            SelectedGrid = grid;
            return;
        }

        if (_imageStream is not null)
        {
            _logger.LogInformation("Generating new DrillGrid for size {CellSize}", cellSize);
            try 
            {
                // Resolve a logger specifically for the new DrillGrid instance
                var gridLogger = _serviceProvider.GetRequiredService<ILogger<DrillGrid>>();
                var newGrid = await Task.Run(() => new DrillGrid(_colorMapService, _imageStream, cellSize, 0, 0, gridLogger));
                _grids[cellSize] = newGrid;
                SelectedGrid = newGrid;
                _logger.LogInformation("DrillGrid generated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate DrillGrid for size {CellSize}", cellSize);
                throw;
            }
        }
        else
        {
             _logger.LogWarning("SelectGridAsync called but _imageStream is null");
        }
    }
}
