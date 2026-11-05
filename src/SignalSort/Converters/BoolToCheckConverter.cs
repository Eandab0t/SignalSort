using System;
using System.Globalization;
using System.Windows.Data;

namespace SignalSort.Converters;

/// <summary>
/// Converts a bool to a nullable bool for CheckBox.IsChecked binding.
/// </summary>
public class BoolToCheckConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b;
        return false;
    }
}
