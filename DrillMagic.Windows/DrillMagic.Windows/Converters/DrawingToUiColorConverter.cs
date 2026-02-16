using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using System;
using System.Drawing;
using Color = Windows.UI.Color;

namespace DrillMagic.Windows.Converters;

public partial class DrawingToUiColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is System.Drawing.Color drawingColor)
        {
            return new Color { A = drawingColor.A, R = drawingColor.R, G = drawingColor.G, B = drawingColor.B };
        }
        // Use Color.FromArgb instead of Colors.Transparent to avoid WinUI COM initialization
        return Color.FromArgb(0, 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}