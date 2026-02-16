using DrillMagic.Core.Constants;
using DrillMagic.Core.Types;
using DrillMagic.Windows.Models;
using DrillMagic.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Windows.UI;
using Windows.UI.Xaml;

namespace DrillMagic.Windows.Controls;
public sealed partial class ColorSummaryLegend : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(nameof(Summary), typeof(Dictionary<System.Drawing.Color, int>), typeof(ColorSummaryLegend), new PropertyMetadata(null, OnSummaryChanged));

    public Dictionary<System.Drawing.Color, int> Summary
    {
        get => (Dictionary<System.Drawing.Color, int>)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public ObservableCollection<ColorSummaryItem> ColorSummary { get; } = new();

    public int TotalDrills => ColorSummary.Sum(c => c.Quantity);

    private readonly ISharedInteractionService _sharedInteractionService;
    private readonly IColorMapService _colorMapService;

    public ColorSummaryLegend()
    {
        this.InitializeComponent();
        _sharedInteractionService = App.Current.Services.GetRequiredService<ISharedInteractionService>();
        _colorMapService = App.Current.Services.GetRequiredService<IColorMapService>();
        
        ColorSummary.CollectionChanged += (s, e) =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalDrills)));
            // Also need to notify for the count if you are not using x:Bind on the collection directly
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorSummary)));
        };
    }

    private void ColorSummaryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is ColorSummaryItem selectedItem)
        {
            _sharedInteractionService.HighlightedColor = _colorMapService.GetDMCColor(selectedItem.DMCNumber);
        }
        else if (e.RemovedItems.Any())
        {
            // If selection is cleared, clear the highlight
            _sharedInteractionService.HighlightedColor = null;
        }
    }

    private static void OnSummaryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ColorSummaryLegend)d;
        control.ColorSummary.Clear();
        if (e.NewValue is Dictionary<System.Drawing.Color, int> summary)
        {
            // Symbols can be customized further. For now, we'll use a simple incrementing character.
            int currentSymbolIndex = 0;
            foreach (var entry in summary.OrderByDescending(kvp => kvp.Value))
            {
                // Accessing the instance service from the static context via the control instance
                var dmcColor = control._colorMapService.DefaultColorMap.Values.FirstOrDefault(c => c.Color == entry.Key);
                var name = dmcColor?.Name ?? "Unknown";
                var hex = dmcColor?.Hex ?? $"#{entry.Key.R:X2}{entry.Key.G:X2}{entry.Key.B:X2}";
                var rgb = $"RGB({entry.Key.R}, {entry.Key.G}, {entry.Key.B})";

                control.ColorSummary.Add(new ColorSummaryItem(
                    Color.FromArgb(entry.Key.A, entry.Key.R, entry.Key.G, entry.Key.B),
                    entry.Value,
                    name,
                    hex,
                    rgb,
                    DrillSymbols.Symbols[currentSymbolIndex % DrillSymbols.Symbols.Length].ToString(),
                    dmcColor?.DMCNumber ?? uint.MaxValue
                ));

                currentSymbolIndex++;
            }
        }
    }

    private void AssignSymbols(IEnumerable<ColorSummaryItem> items)
    {
        var sortedSummary = items.OrderByDescending(i => i.Quantity).ToList();
        ColorSummary.Clear();
        // Manually adding to ObservableCollection since AddRange isn't standard
        foreach (var item in sortedSummary)
        {
            ColorSummary.Add(item);
        }

        var symbolIndex = 0;
        foreach (var item in sortedSummary)
        {
            item.Symbol = DrillSymbols.Symbols[symbolIndex % DrillSymbols.Symbols.Length];
            symbolIndex++;
        }
    }
}
