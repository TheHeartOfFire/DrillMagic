using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace DrillMagic.Windows.Converters;

public partial class ColorToForegroundConverter : IValueConverter
{
    // Make this public or internal so it can be tested without creating a Brush
    public static Color GetContrastColor(Color bg)
    {
        double luminance = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B);
        // Avoid using Microsoft.UI.Colors to allow unit testing without full generic app host
        return luminance > 186 ? Color.FromArgb(255, 0, 0, 0) : Color.FromArgb(255, 255, 255, 255);
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Color color)
        {
            return new SolidColorBrush(GetContrastColor(color));
        }
        return null; // or DependencyProperty.UnsetValue
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}