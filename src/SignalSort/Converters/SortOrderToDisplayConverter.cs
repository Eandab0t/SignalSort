using System;
using System.Globalization;
using System.Windows.Data;
using SignalSort.Models;

namespace SignalSort.Converters;

/// <summary>
/// Converts a SortOrder enum value to its display name.
/// </summary>
public class SortOrderToDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SortOrder order)
        {
            return order.GetDisplayName();
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
