using System;
using System.ComponentModel;
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
    private bool _closeAfterOperation;

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme();
        StateChanged += (_, _) => UpdateMaximizeGlyph();
        Closing += Window_Closing;
    }

    /// <summary>
    /// Graceful shutdown: if an operation is in flight, cancel it and wait
    /// until it reaches its journaled stop point before closing.
    /// </summary>
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_closeAfterOperation || DataContext is not MainViewModel vm)
            return;

        if (!vm.IsBusy)
            return;

        // Cancel and hold the window open until the operation stops.
        e.Cancel = true;
        _closeAfterOperation = true;
        vm.RequestShutdown();

        _ = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (vm.IsBusy && DateTime.UtcNow < deadline)
                await Task.Delay(100);

            _ = Dispatcher.BeginInvoke(() =>
            {
                _closeAfterOperation = false;
                Close();
            });
        });
    }

    private void ApplyTheme()
    {
        if (DataContext is MainViewModel vm)
            App.ApplyTheme(vm.IsDarkTheme);
    }

    // CHROME
    // Window chrome buttons and dragging.

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click the header to toggle maximize.
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
            return;
        }

        // DragMove throws if the button was released by the time we call it.
        if (WindowState == WindowState.Maximized)
            return;

        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

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

    // NAV
    // Side-rail navigation.

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

    // DRAGDROP
    // Folder drop handling.

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

    // INSPECTOR
    // Purpose correction.

    private void ApplyPurpose_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedItem == null)
            return;

        if (FindName("PurposeBox") is not System.Windows.Controls.ComboBox box || string.IsNullOrWhiteSpace(box.Text))
            return;

        vm.CorrectPurpose(vm.SelectedItem, box.Text);
    }
}
