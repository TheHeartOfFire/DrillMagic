using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DrillMagic.Windows.Converters;

public partial class ObjectToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isNull = value == null;
        if (parameter is string paramStr && (paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase) || paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase)))
        {
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}