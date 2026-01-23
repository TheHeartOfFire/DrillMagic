using DrillMagic.Core.Types;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;

namespace DrillMagic.Windows.Services;

public enum InteractionMode
{
    Navigate,
    Coloring,
    ColorInspector
}

public partial class SharedInteractionService : ISharedInteractionService
{
    private InteractionMode _currentMode = InteractionMode.Navigate;
    private DMCColor? _highlightedColor;

    public event PropertyChangedEventHandler? PropertyChanged;

    public InteractionMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode != value)
            {
                _currentMode = value;
                OnPropertyChanged();
            }
        }
    }

    private DMCColor _inspectedColor = new("Invalid", uint.MaxValue, "Invalid");
    public DMCColor InspectedColor
    {
        get => _inspectedColor;
        set
        {
            if (_inspectedColor != value)
            {
                _inspectedColor = value;
                SelectedColor = value.Color;
                OnPropertyChanged();
            }
        }
    }

    public DMCColor? HighlightedColor
    {
        get => _highlightedColor;
        set
        {
            if (_highlightedColor != value)
            {
                _highlightedColor = value;
                if (value is not null)
                    SelectedColor = value.Color;
                OnPropertyChanged();
            }
        }
    }
    private System.Drawing.Color _selectedColor = System.Drawing.Color.Black;
    public System.Drawing.Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (_selectedColor != value)
            {
                _selectedColor = value;
                OnPropertyChanged();
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}