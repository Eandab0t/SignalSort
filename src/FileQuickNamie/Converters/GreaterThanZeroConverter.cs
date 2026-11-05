using System;
using System.Globalization;
using System.Windows.Data;

namespace SmartFileClassifier.Converters;

/// <summary>
/// Converts an integer to a boolean: true when greater than zero.
/// Used by the dock status indicator to show a conflict state.
/// </summary>
public class GreaterThanZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return false;

        if (int.TryParse(value.ToString(), out var number))
            return number > 0;

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
