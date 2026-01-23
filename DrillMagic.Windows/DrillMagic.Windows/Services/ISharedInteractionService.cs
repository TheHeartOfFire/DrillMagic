using DrillMagic.Core.Types;
using System.ComponentModel;

namespace DrillMagic.Windows.Services;

public interface ISharedInteractionService : INotifyPropertyChanged
{
    InteractionMode CurrentMode { get; set; }
    DMCColor InspectedColor { get; set; }
    DMCColor? HighlightedColor { get; set; }
    System.Drawing.Color SelectedColor { get; set; }
}