using DrillMagic.Core.Types;
using Microsoft.Extensions.Logging;
using System;
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
    private readonly ILogger<SharedInteractionService> _logger;
    private InteractionMode _currentMode = InteractionMode.Navigate;
    private DMCColor? _highlightedColor;
    private DMCColor _inspectedColor = new("Invalid", uint.MaxValue, "Invalid");
    private System.Drawing.Color _selectedColor = System.Drawing.Color.Black;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SharedInteractionService(ILogger<SharedInteractionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public InteractionMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode != value)
            {
                _logger.LogInformation("Changing InteractionMode from {OldMode} to {NewMode}", _currentMode, value);
                _currentMode = value;
                OnPropertyChanged();
            }
        }
    }

    public DMCColor InspectedColor
    {
        get => _inspectedColor;
        set
        {
            if (_inspectedColor != value)
            {
                _logger.LogDebug("InspectedColor changed to {ColorName} ({DMCNumber})", value.Name, value.DMCNumber);
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
                _logger.LogDebug("HighlightedColor changed to {ColorName}", value?.Name ?? "null");
                _highlightedColor = value;
                if (value is not null)
                    SelectedColor = value.Color;
                OnPropertyChanged();
            }
        }
    }

    public System.Drawing.Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (_selectedColor != value)
            {
                _logger.LogTrace("SelectedColor changed to {Color}", value);
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