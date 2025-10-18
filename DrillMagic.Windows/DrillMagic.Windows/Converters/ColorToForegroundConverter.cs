using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace DrillMagic.Windows.Converters;

public partial class ColorToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Color color)
        {
            // Calculate perceived brightness (luminance) using the standard formula.
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B);

            // Use a threshold of 186, which is a common value for this calculation,
            // to determine whether the color is light or dark.
            if (luminance > 186)
            {
                return new SolidColorBrush(Colors.Black); // Bright background, so use dark text.
            }
            else
            {
                return new SolidColorBrush(Colors.White); // Dark background, so use light text.
            }
        }

        // Return a default brush if the value is not a Color.
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}