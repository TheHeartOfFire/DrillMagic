using DrillMagic.Core.Types;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DrillMagic.Windows.Services;

public enum InteractionMode
{
    Navigate,
    Coloring,
    ColorInspector
}

public partial class SharedInteractionService : INotifyPropertyChanged
{
    private InteractionMode _currentMode = InteractionMode.Navigate;
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

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}