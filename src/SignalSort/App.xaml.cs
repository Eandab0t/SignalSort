using System;
using System.Windows;

namespace SignalSort;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static void ApplyTheme(bool isDark)
    {
        var app = Application.Current;
        var themeUri = new Uri(isDark ? "/Themes/Dark.xaml" : "/Themes/Light.xaml", UriKind.Relative);

        var themeDict = new ResourceDictionary { Source = themeUri };

        if (app.Resources.MergedDictionaries.Count > 0)
            app.Resources.MergedDictionaries[0] = themeDict;
        else
            app.Resources.MergedDictionaries.Add(themeDict);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
}
