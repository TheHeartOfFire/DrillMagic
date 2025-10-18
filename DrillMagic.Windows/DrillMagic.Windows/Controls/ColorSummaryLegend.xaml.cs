using DrillMagic.Core.Types;
using DrillMagic.Windows.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.UI;
using Color = Windows.UI.Color;

namespace DrillMagic.Windows.Controls;

public sealed partial class ColorSummaryLegend : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(nameof(Summary), typeof(Dictionary<Color, int>), typeof(ColorSummaryLegend), new PropertyMetadata(null, OnSummaryChanged));

    public Dictionary<System.Drawing.Color, int> Summary
    {
        get => (Dictionary<System.Drawing.Color, int>)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public ObservableCollection<ColorSummaryItem> ColorSummary { get; } = new();

    public int TotalDrills => ColorSummary.Sum(c => c.Quantity);

    public static readonly string[] Symbols =
    [
        // A-Z (Single characters)
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        // a-z (Single characters)
        "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
        // 0-9 (Single characters)
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        // Standard keyboard symbols (13 characters)
        "?", "!", ":", "(", "{", "&", "@", "#", "*", "=", "/", "-", "—",
        // Remaining miscellaneous symbols (18 characters)
        "□", "△", "♠", "♣", "♥", "♦", "☆", "☾", "▱", "◫", "⌂", "⬡", "±", "÷", "∞", "⌽", "✓", "→",
        // AA-ZZ (Double characters)
        .. Enumerable.Range('A', 'Z' - 'A' + 1)
            .Select(c1 => (char)c1)
            .SelectMany(c1 => Enumerable.Range('A', 'Z' - 'A' + 1)
                .Select(c2 => (char)c2), (c1, c2) => $"{c1}{c2}"),
        // aa-zz (Double characters)
        .. Enumerable.Range('a', 'z' - 'a' + 1)
            .Select(c1 => (char)c1)
            .SelectMany(c1 => Enumerable.Range('a', 'z' - 'a' + 1)
                .Select(c2 => (char)c2), (c1, c2) => $"{c1}{c2}"),
    ]; // Total count 1,399

    public ColorSummaryLegend()
    {
        this.InitializeComponent();
        ColorSummary.CollectionChanged += (s, e) =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalDrills)));
            // Also need to notify for the count if you are not using x:Bind on the collection directly
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorSummary)));
        };
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
                var dmcColor = ColorMap.DefaultColorMap.Values.FirstOrDefault(c => c.Color == entry.Key);
                var name = dmcColor?.Name ?? "Unknown";
                var hex = dmcColor?.Hex ?? $"#{entry.Key.R:X2}{entry.Key.G:X2}{entry.Key.B:X2}";
                var rgb = $"RGB({entry.Key.R}, {entry.Key.G}, {entry.Key.B})";

                control.ColorSummary.Add(new ColorSummaryItem(
                    Color.FromArgb(entry.Key.A, entry.Key.R, entry.Key.G, entry.Key.B),
                    entry.Value,
                    name,
                    hex,
                    rgb,
                    Symbols[currentSymbolIndex].ToString(),
                    dmcColor?.DMCNumber ?? uint.MaxValue
                ));

                currentSymbolIndex++;
            }
        }
    }
}
