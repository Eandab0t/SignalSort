using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SmartFileClassifier.Models;
using SmartFileClassifier.Naming;
using SmartFileClassifier.Services;

namespace SmartFileClassifier.ViewModels;

/// <summary>
/// The main view model for the application.
/// Implements INotifyPropertyChanged for data binding.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly MetadataExtractor _metadataExtractor;
    private readonly FileClassifier _classifier;
    private PreviewEngine? _previewEngine;
    private readonly RenameEngine _renameEngine;

    private ObservableCollection<FileClassification> _classifications = new();
    private ObservableCollection<NamingStyle> _namingStyles = new();
    private ObservableCollection<SortOrder> _sortOrders = new();

    private NamingStyle _selectedNamingStyle = NamingStyle.CategoryNumber;
    private SortOrder _selectedSortOrder = SortOrder.OriginalName;
    private string _selectedFolderPath = string.Empty;
    private bool _isScanning;
    private bool _canRename;
    private string _logOutput = string.Empty;
    private string _statusMessage = "Ready";

    // Toggle options
    private bool _isDarkTheme = false;
    private bool _isRecursiveScan = false;
    private bool _includeHiddenFiles = false;
    private bool _includeSystemFiles = false;
    private bool _preserveExtension = true;
    private bool _previewOnly = false;
    private bool _caseSensitiveNaming = false;
    private bool _autoCloseAfterRename = false;

    public ObservableCollection<FileClassification> Classifications
    {
        get => _classifications;
        set { _classifications = value; OnPropertyChanged(); }
    }

    public ObservableCollection<NamingStyle> NamingStyles
    {
        get => _namingStyles;
        set { _namingStyles = value; OnPropertyChanged(); }
    }

    public ObservableCollection<SortOrder> SortOrders
    {
        get => _sortOrders;
        set { _sortOrders = value; OnPropertyChanged(); }
    }

    public NamingStyle SelectedNamingStyle
    {
        get => _selectedNamingStyle;
        set
        {
            _selectedNamingStyle = value;
            OnPropertyChanged();
            UpdatePreviews();
        }
    }

    public SortOrder SelectedSortOrder
    {
        get => _selectedSortOrder;
        set
        {
            _selectedSortOrder = value;
            OnPropertyChanged();
            ApplySorting();
        }
    }

    public string SelectedFolderPath
    {
        get => _selectedFolderPath;
        set { _selectedFolderPath = value; OnPropertyChanged(); }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); }
    }

    public bool CanRename
    {
        get => _canRename;
        set { _canRename = value; OnPropertyChanged(); }
    }

    public string LogOutput
    {
        get => _logOutput;
        set { _logOutput = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    // Toggle Properties

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            _isDarkTheme = value;
            OnPropertyChanged();
            App.ApplyTheme(value);
        }
    }

    public bool IsRecursiveScan
    {
        get => _isRecursiveScan;
        set { _isRecursiveScan = value; OnPropertyChanged(); }
    }

    public bool IncludeHiddenFiles
    {
        get => _includeHiddenFiles;
        set { _includeHiddenFiles = value; OnPropertyChanged(); }
    }

    public bool IncludeSystemFiles
    {
        get => _includeSystemFiles;
        set { _includeSystemFiles = value; OnPropertyChanged(); }
    }

    public bool PreserveExtension
    {
        get => _preserveExtension;
        set { _preserveExtension = value; OnPropertyChanged(); }
    }

    public bool PreviewOnly
    {
        get => _previewOnly;
        set { _previewOnly = value; OnPropertyChanged(); }
    }

    public bool CaseSensitiveNaming
    {
        get => _caseSensitiveNaming;
        set { _caseSensitiveNaming = value; OnPropertyChanged(); }
    }

    public bool AutoCloseAfterRename
    {
        get => _autoCloseAfterRename;
        set { _autoCloseAfterRename = value; OnPropertyChanged(); }
    }

    // Commands
    public RelayCommand BrowseCommand { get; }
    public RelayCommand RescanCommand { get; }
    public RelayCommand RenameAllCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    public MainViewModel()
    {
        _metadataExtractor = new MetadataExtractor();
        _classifier = new FileClassifier(_metadataExtractor);
        _renameEngine = new RenameEngine();
        _renameEngine.OnLog = Log;

        BrowseCommand = new RelayCommand(_ => BrowseForFolder(), _ => !IsScanning);
        RescanCommand = new RelayCommand(_ => RescanFolder(), _ => !IsScanning && !string.IsNullOrEmpty(SelectedFolderPath));
        RenameAllCommand = new RelayCommand(_ => RenameAll(), _ => CanRename && !IsScanning);
        ClearLogCommand = new RelayCommand(_ => ClearLog(), _ => true);
        ToggleThemeCommand = new RelayCommand(_ => IsDarkTheme = !IsDarkTheme, _ => true);

        // Populate naming styles
        NamingStyles = new ObservableCollection<NamingStyle>(Enum.GetValues<NamingStyle>());
        SortOrders = new ObservableCollection<SortOrder>(Enum.GetValues<SortOrder>());
    }

    private void BrowseForFolder()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a folder to scan",
            FileName = "folder",
            ValidateNames = false,
            CheckFileExists = false,
            CheckPathExists = true,
            AddToRecent = false
        };

        if (dialog.ShowDialog() == true)
        {
            var folderPath = Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(folderPath))
            {
                SelectedFolderPath = folderPath;
                _ = ScanFolderAsync();
            }
        }
    }

    private async Task ScanFolderAsync()
    {
        if (string.IsNullOrEmpty(SelectedFolderPath))
            return;

        IsScanning = true;
        CanRename = false;
        StatusMessage = "Scanning files...";
        LogOutput = string.Empty;

        try
        {
            _previewEngine = new PreviewEngine(_classifier, SelectedNamingStyle);

            ObservableCollection<FileClassification> results;
            if (IsRecursiveScan)
                results = await _previewEngine.ScanFolderRecursiveAsync(
                    SelectedFolderPath, IncludeHiddenFiles, IncludeSystemFiles);
            else
                results = await _previewEngine.ScanFolderAsync(SelectedFolderPath, IncludeHiddenFiles, IncludeSystemFiles);

            Classifications = results;

            // Apply sorting
            ApplySorting();

            CanRename = Classifications.Any();
            StatusMessage = Classifications.Count > 0
                ? $"Found {Classifications.Count} files"
                : "No files found";

            Log($"Scan complete: {Classifications.Count} files found in '{SelectedFolderPath}'");
        }
        catch (Exception ex)
        {
            Log($"Error during scan: {ex.Message}");
            StatusMessage = "Scan failed";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async void RescanFolder()
    {
        await ScanFolderAsync();
    }

    private void UpdatePreviews()
    {
        if (_previewEngine == null || Classifications.Count == 0)
            return;

        _previewEngine.RegenerateNames(Classifications, SelectedNamingStyle, PreserveExtension);
        Log($"Naming style updated to: {SelectedNamingStyle.GetDisplayName()}");
    }

    private void ApplySorting()
    {
        if (Classifications == null || Classifications.Count == 0)
            return;

        var sorted = SelectedSortOrder switch
        {
            SortOrder.Category => Classifications.OrderBy(c => c.Category).ThenBy(c => c.Purpose).ToList(),
            SortOrder.Purpose => Classifications.OrderBy(c => c.Purpose).ThenBy(c => c.Category).ToList(),
            SortOrder.NewName => Classifications.OrderBy(c => c.NewName).ToList(),
            _ => Classifications.OrderBy(c => c.OriginalName).ToList()
        };

        Classifications = new ObservableCollection<FileClassification>(sorted);
    }

    private void RenameAll()
    {
        if (Classifications.Count == 0)
            return;

        IsScanning = true;
        CanRename = false;
        StatusMessage = PreviewOnly ? "Previewing rename..." : "Renaming files...";
        LogOutput = string.Empty;

        try
        {
            if (PreviewOnly)
            {
                // Dry run - just log what would happen
                foreach (var item in Classifications.Where(c => c.IsSelected))
                {
                    Log($"[DRY RUN] Would rename: {item.OriginalName} -> {item.NewName}");
                }
                Log($"--- Preview complete: {Classifications.Count(c => c.IsSelected)} files would be renamed ---");
                StatusMessage = $"Preview: {Classifications.Count(c => c.IsSelected)} files";
            }
            else
            {
                var result = _renameEngine.RenameAll(Classifications);
                StatusMessage = result.IsSuccess
                    ? $"Renamed {result.SuccessfulRenames} files successfully"
                    : $"Renamed {result.SuccessfulRenames}, {result.FailedRenames} failed";

                if (AutoCloseAfterRename && result.IsSuccess)
                {
                    Log("Auto-closing after successful rename..." + Environment.NewLine);
                    Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    Application.Current.MainWindow?.Close();
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error during rename: {ex.Message}");
            StatusMessage = "Rename failed";
        }
        finally
        {
            IsScanning = false;
            CanRename = Classifications.Count > 0;
        }
    }

    private void ClearLog()
    {
        LogOutput = string.Empty;
    }

    private void Log(string message)
    {
        LogOutput += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
