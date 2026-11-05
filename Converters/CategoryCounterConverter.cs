using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using SmartFileClassifier.Models;

namespace SmartFileClassifier.Converters;

/// <summary>
/// Counts files in a collection that match a specified category.
/// Parameter is the category name to filter by.
/// </summary>
public class CategoryCounterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is System.Collections.IEnumerable items && parameter is string category)
        {
            var count = items.Cast<object>()
                .OfType<FileClassification>()
                .Count(fc => category == "Folder"
                    ? fc.Category != "Audio" && fc.Category != "Video" && fc.Category != "Image"
                    : fc.Category == category);
            return count.ToString();
        }
        return "0";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
