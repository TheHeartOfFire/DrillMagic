using DrillMagic.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace DrillMagic.Windows.Services;

public interface IDrillGridManager : INotifyPropertyChanged
{
    DrillGrid? SelectedGrid { get; }
    ObservableCollection<uint> AvailableCellSizes { get; }
    Task LoadImageAsync(string imagePath, CancellationToken cancellationToken = default);
    Task SelectGridAsync(uint cellSize);
}
