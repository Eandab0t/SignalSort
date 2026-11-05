using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SignalSort.Models;
using SignalSort.ViewModels;

namespace SignalSort.Views;

/// <summary>
/// Signal Rail main window. Code-behind handles only window chrome, drag
/// navigation and inspector actions - all state lives in MainViewModel.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme();
        StateChanged += (_, _) => UpdateMaximizeGlyph();
    }

    private void ApplyTheme()
    {
        if (DataContext is MainViewModel vm)
            App.ApplyTheme(vm.IsDarkTheme);
    }

    // ------------------------------------------------------------ chrome

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaximizeGlyph()
    {
        if (FindName("MaximizeButton") is not System.Windows.Controls.Button button)
            return;

        button.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    // ------------------------------------------------------------ navigation

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton radio && radio.Tag is string target &&
            DataContext is MainViewModel vm)
        {
            vm.ActiveFilter = target;
        }
    }

    private void EmptyState_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.BrowseCommand.Execute(null);
    }

    // ------------------------------------------------------------ drag & drop

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return;

        var path = paths[0];
        if (!System.IO.Directory.Exists(path))
            path = System.IO.Path.GetDirectoryName(path) ?? "";

        if (string.IsNullOrEmpty(path) || DataContext is not MainViewModel vm)
            return;

        vm.SelectedFolderPath = path;
        _ = vm.ScanFromDropAsync(path);
    }

    // ------------------------------------------------------------ inspector

    private void ApplyPurpose_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedItem == null)
            return;

        if (FindName("PurposeBox") is not System.Windows.Controls.ComboBox box || string.IsNullOrWhiteSpace(box.Text))
            return;

        vm.CorrectPurpose(vm.SelectedItem, box.Text);
    }
}
