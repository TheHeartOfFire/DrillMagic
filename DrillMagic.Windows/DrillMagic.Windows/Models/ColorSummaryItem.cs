

using Windows.UI;

namespace DrillMagic.Windows.Models;
public class ColorSummaryItem
{
    public Color Color { get; set; }
    public int Quantity { get; set; }
    public string Name { get; set; }
    public string Hex { get; set; }
    public string Rgb { get; set; }
    public string Symbol { get; set; }
    public uint DMCNumber { get; set; }

    public ColorSummaryItem(Color color, int quantity, string name, string hex, string rgb, string symbol, uint dMCNumber)
    {
        Color = color;
        Quantity = quantity;
        Name = name;
        Hex = hex;
        Rgb = rgb;
        Symbol = symbol;
        DMCNumber = dMCNumber;
    }

    // A parameterless constructor is required for XAML instantiation
    public ColorSummaryItem()
    {
        Name = "";
        Hex = "";
        Rgb = "";
        Symbol = "";
        DMCNumber = uint.MaxValue;
    }
}
