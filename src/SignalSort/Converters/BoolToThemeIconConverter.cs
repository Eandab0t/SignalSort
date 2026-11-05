using System;
using System.Globalization;
using System.Windows.Data;

namespace SignalSort.Converters;

/// <summary>
/// Converts a bool (isDarkTheme) to a symbol icon character from Segoe MDL2 Assets.
/// </summary>
public class BoolToThemeIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isDark && isDark)
            return "\uE700"; // Back icon for dark mode toggle (moon concept)
        return "\uE747";       // Forward/icon for light mode toggle (sun concept)
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a bool to a display string (e.g., "Dark" / "Light").
/// </summary>
public class BoolToThemeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isDark && isDark)
            return "Dark";
        return "Light";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text)
            return text == "Dark";
        return false;
    }
}
