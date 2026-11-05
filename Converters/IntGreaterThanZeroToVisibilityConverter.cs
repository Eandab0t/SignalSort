using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmartFileClassifier.Converters;

/// <summary>
/// Converts an integer to Visibility: greater than 0 maps to Visible, otherwise Collapsed.
/// </summary>
public class IntGreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
