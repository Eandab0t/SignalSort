using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using SmartFileClassifier.Core;
using SmartFileClassifier.Models;

namespace SmartFileClassifier.ViewModels;

/// <summary>
/// The main view model. Orchestrates scanning, classification preview,
/// rename planning/execution and undo. All long-running work is async with
/// progress reporting and cancellation; the UI is updated on the dispatcher.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly MetadataReader _metadataReader = new();
    private readonly FileClassifier _classifier;
    private readonly FileScanner _scanner;
    private readonly NameGenerator _nameGenerator = new();
    private readonly RenamePlanner _planner = new();
    private readonly RenameJournal _journal;
    private readonly AppSettings _settings;

    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _renameCts;

    private ObservableCollection<FileClassification> _items = new();
    private ICollectionView? _itemsView;

    private string _selectedFolderPath = "";
    private bool _isBusy;
    private double _progressPercent;
    private string _statusMessage = "Select a folder to begin";
    private string _logOutput = "";
    private bool _isDarkTheme = true;
    private bool _isRecursiveScan;
    private bool _includeHiddenFiles;
    private bool _includeSystemFiles;
    private bool _preserveExtension = true;
    private NamingStyle _selectedNamingStyle = NamingStyle.CategoryPrefixed;
    private SortOrder _selectedSortOrder = SortOrder.OriginalName;
    private string _searchText = "";
    private string _activeFilter = "All Files";
    private FileClassification? _selectedItem;
    private int _collisionCount;
    private bool _canUndo;
    private string _historyText = "";

    public MainViewModel()
    {
        _classifier = new FileClassifier(_metadataReader);
        _scanner = new FileScanner(_classifier, _metadataReader);
        _journal = new RenameJournal();
        _settings = AppSettings.Load();

        // Restore persisted state.
        _isDarkTheme = _settings.IsDarkTheme;
        _selectedFolderPath = _settings.LastFolder ?? "";
        _isRecursiveScan = _settings.RecursiveScan;
        _includeHiddenFiles = _settings.IncludeHiddenFiles;
        _includeSystemFiles = _settings.IncludeSystemFiles;
        _preserveExtension = _settings.PreserveExtension;
        _selectedNamingStyle = _settings.NamingStyle;
        _selectedSortOrder = _settings.SortOrder;

        Items = new ObservableCollection<FileClassification>();

        BrowseCommand = new RelayCommand(_ => BrowseForFolder(), _ => !IsBusy);
        ScanCommand = new RelayCommand(_ => _ = ScanAsync(), _ => !IsBusy && Directory.Exists(SelectedFolderPath));
        CancelScanCommand = new RelayCommand(_ => _scanCts?.Cancel(), _ => IsBusy && _scanCts != null);
        RenameCommand = new RelayCommand(_ => _ = RenameSelectedAsync(), _ => !IsBusy && SelectedCount > 0);
        PreviewCommand = new RelayCommand(_ => PreviewPlan(), _ => !IsBusy && SelectedCount > 0);
        UndoCommand = new RelayCommand(_ => _ = UndoLastAsync(), _ => !IsBusy && CanUndo);
        ClearLogCommand = new RelayCommand(_ => LogOutput = "", _ => true);
        ToggleThemeCommand = new RelayCommand(_ => IsDarkTheme = !IsDarkTheme, _ => true);
        SelectAllCommand = new RelayCommand(p => SetAllSelection(true), _ => !IsBusy);
        SelectNoneCommand = new RelayCommand(_ => SetAllSelection(false), _ => !IsBusy);

        CanUndo = _journal.CanUndo();
        LoadHistoryText();

        App.ApplyTheme(_isDarkTheme);
    }

    // ------------------------------------------------------------------ data

    public ObservableCollection<FileClassification> Items
    {
        get => _items;
        private set
        {
            _items = value;
            _itemsView = CollectionViewSource.GetDefaultView(_items);
            _itemsView.Filter = FilterItem;
            _itemsView.SortDescriptions.Clear();
            ApplySortDescriptions();
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilesShown));
        }
    }

    public ICollectionView ItemsView => _itemsView ?? CollectionViewSource.GetDefaultView(Items);

    // ------------------------------------------------------------- properties

    public string SelectedFolderPath
    {
        get => _selectedFolderPath;
        set { _selectedFolderPath = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        set { _progressPercent = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string LogOutput
    {
        get => _logOutput;
        set { _logOutput = value; OnPropertyChanged(); }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            _isDarkTheme = value;
            OnPropertyChanged();
            App.ApplyTheme(value);
            _settings.IsDarkTheme = value;
            _settings.Save();
        }
    }

    public bool IsRecursiveScan
    {
        get => _isRecursiveScan;
        set { _isRecursiveScan = value; OnPropertyChanged(); Persist(); }
    }

    public bool IncludeHiddenFiles
    {
        get => _includeHiddenFiles;
        set { _includeHiddenFiles = value; OnPropertyChanged(); Persist(); }
    }

    public bool IncludeSystemFiles
    {
        get => _includeSystemFiles;
        set { _includeSystemFiles = value; OnPropertyChanged(); Persist(); }
    }

    public bool PreserveExtension
    {
        get => _preserveExtension;
        set
        {
            _preserveExtension = value;
            OnPropertyChanged();
            Persist();
            RegenerateNames();
        }
    }

    public NamingStyle SelectedNamingStyle
    {
        get => _selectedNamingStyle;
        set
        {
            _selectedNamingStyle = value;
            OnPropertyChanged();
            Persist();
            RegenerateNames();
        }
    }

    public SortOrder SelectedSortOrder
    {
        get => _selectedSortOrder;
        set { _selectedSortOrder = value; OnPropertyChanged(); Persist(); ApplySorting(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); ItemsView.Refresh(); OnPropertyChanged(nameof(FilesShown)); }
    }

    public string ActiveFilter
    {
        get => _activeFilter;
        set { _activeFilter = value; OnPropertyChanged(); ItemsView.Refresh(); OnPropertyChanged(nameof(FilesShown)); }
    }
    public FileClassification? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem != null)
                _selectedItem.IsSelectedForInspect = false;
            _selectedItem = value;
            if (_selectedItem != null)
                _selectedItem.IsSelectedForInspect = true;
            OnPropertyChanged();
        }
    }

    public int CollisionCount
    {
        get => _collisionCount;
        set { _collisionCount = value; OnPropertyChanged(); }
    }

    public bool CanUndo
    {
        get => _canUndo;
        set { _canUndo = value; OnPropertyChanged(); }
    }

    public string HistoryText
    {
        get => _historyText;
        set { _historyText = value; OnPropertyChanged(); }
    }

    // ------------------------------------------------------------- derived

    public IReadOnlyList<string> Filters => new[]
        { "All Files", "Images", "Audio", "Video", "Documents", "Folders", "Conflicts" };

    /// <summary>
    /// Naming style values exposed to the ComboBox in the toolbar.
    /// </summary>
    public IReadOnlyList<NamingStyle> NamingStyles => Enum.GetValues<NamingStyle>();

    /// <summary>
    /// Sort order values exposed to the ComboBox in the toolbar.
    /// </summary>
    public IReadOnlyList<SortOrder> SortOrders => Enum.GetValues<SortOrder>();

    public int SelectedCount => Items.Count(i => i.IsSelected);
    public int FilesShown => ItemsView.Cast<object>().Count();
    public int ChangesCount => Items.Count(i => i.IsSelected &&
        !string.Equals(i.OriginalName, i.NewName, StringComparison.OrdinalIgnoreCase));

    public bool HasFiles => Items.Count > 0;

    /// <summary>History panel is active (nav item).</summary>
    public bool IsHistoryPanel => ActiveFilter == "History";

    /// <summary>Settings panel is active (nav item).</summary>
    public bool IsSettingsPanel => ActiveFilter == "Settings";

    /// <summary>
    /// Purposes the inspector offers for correction, grouped by category.
    /// </summary>
    public IReadOnlyList<string> KnownPurposes => SelectedItem?.Category switch
    {
        "Audio" => new[] { "VoiceMemo", "SoundEffect", "Stinger", "Song", "UnknownAudio" },
        "Video" => new[] { "ScreenRec", "CameraFootage", "Clip", "Short", "Recording", "UnknownVideo" },
        "Image" => new[] { "Photo", "Icon", "Thumbnail" },
        "Folder" => new[] { "MediaDump", "AudioCollection", "VideoSet", "ImagePack", "Empty" },
        "Archive" => new[] { "Archive" },
        _ => new[] { "Document" }
    };

    // ------------------------------------------------------------- commands

    public RelayCommand BrowseCommand { get; }
    public RelayCommand ScanCommand { get; }
    public RelayCommand CancelScanCommand { get; }
    public RelayCommand RenameCommand { get; }
    public RelayCommand PreviewCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand ClearLogCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }

    // ------------------------------------------------------------- scanning

    /// <summary>
    /// Entry point for folder drag-and-drop: sets the folder and scans.
    /// </summary>
    public Task ScanFromDropAsync(string folderPath)
    {
        SelectedFolderPath = folderPath;
        return ScanAsync();
    }

    private void BrowseForFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select a folder to scan"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFolderPath = dialog.FolderName;
            _ = ScanAsync();
        }
    }

    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath) || !Directory.Exists(SelectedFolderPath))
            return;
        IsBusy = true;
        ProgressPercent = 0;
        StatusMessage = "Scanning...";
        _scanCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                ProgressPercent = p.Percent;
                StatusMessage = $"Scanning... {p.Processed}/{p.Total}  {p.CurrentName}";
            });

            var results = await Task.Run(
                () => _scanner.Scan(
                    SelectedFolderPath,
                    IsRecursiveScan,
                    IncludeHiddenFiles,
                    IncludeSystemFiles,
                    progress,
                    _scanCts.Token),
                _scanCts.Token);

            OnDispatcher(() =>
            {
                Items = new ObservableCollection<FileClassification>(results);
                RegenerateNames();
                ApplySorting();
                SelectedItem = results.FirstOrDefault();
            });

            StatusMessage = $"Found {results.Count} items in {SelectedFolderPath}";
            Log($"Scan complete: {results.Count} items ({results.Count(i => i.Kind == ItemKind.File)} files, {results.Count(i => i.Kind == ItemKind.Folder)} folders)");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled";
            Log("Scan cancelled.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Scan failed";
            Log($"ERROR during scan: {ex.Message}");
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
            ProgressPercent = 0;
            IsBusy = false;
            Persist();
        }
    }

    // ------------------------------------------------------------- naming

    /// <summary>
    /// Regenerates all names, updates the change/collision counts.
    /// </summary>
    private void RegenerateNames()
    {
        _nameGenerator.AssignNames(Items, SelectedNamingStyle, PreserveExtension);
        RecalculateCounts();
        ItemsView.Refresh();
        OnPropertyChanged(nameof(ChangesCount));
    }

    /// <summary>
    /// Applies a purpose correction from the inspector and renames just that item.
    /// </summary>
    public void CorrectPurpose(FileClassification item, string newPurpose)
    {
        if (item == null || string.IsNullOrWhiteSpace(newPurpose)) return;

        item.Purpose = newPurpose.Trim();
        if (item.Confidence == ClassificationConfidence.Strong)
            item.Confidence = ClassificationConfidence.Heuristic; // user-corrected
        item.Signals.Add(new ClassificationSignal("Corrected by user", item.Purpose));

        RegenerateNames();
        Log($"Purpose corrected: {item.OriginalName} -> {item.Purpose}");
    }

    private void PreviewPlan()
    {
        var plan = _planner.Plan(Items.Where(i => i.IsSelected));
        CollisionCount = plan.CollisionCount + plan.CycleCount;

        Log("--- PREVIEW (nothing will be changed) ---");
        foreach (var entry in plan.Entries)
        {
            var name = Path.GetFileName(entry.NewPath);
            switch (entry.Status)
            {
                case RenamePlanStatus.Ready:
                    Log($"[would rename] {Path.GetFileName(entry.OldPath)} -> {name}");
                    break;
                case RenamePlanStatus.Collision:
                    Log($"[COLLISION]    {Path.GetFileName(entry.OldPath)} -> {name}  ({entry.Note})");
                    break;
                case RenamePlanStatus.Cycle:
                    Log($"[CYCLE]        {Path.GetFileName(entry.OldPath)}  ({entry.Note})");
                    break;
                case RenamePlanStatus.NoOp:
                    break;
            }
        }
        Log($"--- {plan.ReadyCount} ready, {plan.CollisionCount} collisions, {plan.CycleCount} cycles ---");
    }

    // ------------------------------------------------------------- renaming

    private async Task RenameSelectedAsync()
    {
        var plan = _planner.Plan(Items.Where(i => i.IsSelected));
        CollisionCount = plan.CollisionCount + plan.CycleCount;

        if (plan.ReadyCount == 0)
        {
            Log("Nothing to rename (no valid changes).");
            StatusMessage = "Nothing to rename";
            return;
        }

        var summary = $"Rename {plan.ReadyCount} item(s)?\n" +
                      $"Collisions (will be skipped): {plan.CollisionCount}\n" +
                      $"Cycles (will be skipped): {plan.CycleCount}";
        if (MessageBox.Show(summary, "Confirm rename", MessageBoxButton.OKCancel, MessageBoxImage.Question,
                MessageBoxResult.Cancel) != MessageBoxResult.OK)
            return;

        IsBusy = true;
        ProgressPercent = 0;
        StatusMessage = "Renaming...";
        _renameCts = new CancellationTokenSource();

        var progress = new Progress<RenameProgress>(p =>
        {
            ProgressPercent = p.Percent;
            StatusMessage = $"Renaming... {p.Processed}/{p.Total}  {p.CurrentName}";
        });

        try
        {
            var executor = new RenameExecutor(_journal);
            var result = await executor.ExecuteAsync(plan, SelectedFolderPath, progress, _renameCts.Token);

            Log($"--- Rename finished: {result.Succeeded} succeeded, {result.Failed} failed ---");
            foreach (var outcome in result.Outcomes.Where(o => !o.Succeeded))
                Log($"[failed] {outcome.OriginalName}: {outcome.Error}");
            foreach (var note in result.Notes)
                Log(note);

            StatusMessage = result.WasCancelled
                ? $"Cancelled - {result.Succeeded} renamed"
                : result.Failed == 0
                    ? $"Renamed {result.Succeeded} items"
                    : $"Renamed {result.Succeeded}, {result.Failed} failed";

            CanUndo = _journal.CanUndo();
            LoadHistoryText();

            if (result.Succeeded > 0)
                await RescanAfterChangeAsync();
        }
        finally
        {
            _renameCts?.Dispose();
            _renameCts = null;
            ProgressPercent = 0;
            IsBusy = false;
        }
    }

    private async Task UndoLastAsync()
    {
        IsBusy = true;
        try
        {
            JournalBatch? batch = null;
            var result = await Task.Run(() => _journal.UndoLast(out batch));
            if (batch == null)
            {
                Log("Nothing to undo.");
                StatusMessage = "Nothing to undo";
                return;
            }

            Log($"--- Undo batch {batch.Id[..Math.Min(8, batch.Id.Length)]}: {result.Restored} restored, {result.Skipped} skipped ---");
            foreach (var note in result.Notes)
                Log(note);

            StatusMessage = $"Undo: {result.Restored} restored, {result.Skipped} skipped";
            CanUndo = _journal.CanUndo();
            LoadHistoryText();

            await RescanAfterChangeAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RescanAfterChangeAsync()
    {
        // After a rename the scanned paths are stale; re-scan to show reality.
        if (Directory.Exists(SelectedFolderPath))
            await ScanAsync();
    }

    // ------------------------------------------------------------- history

    private void LoadHistoryText()
    {
        try
        {
            var batches = _journal.RecentBatches(10);
            if (batches.Count == 0)
            {
                HistoryText = "No rename history yet.";
                return;
            }

            HistoryText = string.Join(Environment.NewLine, batches.Select(b =>
            {
                var time = b.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                var ok = b.Entries.Count(e => e.Succeeded);
                var status = b.Status.ToString();
                return $"{time}  {status,-10} {ok} renames  ({Path.GetFileName(b.Folder)})";
            }));
        }
        catch
        {
            HistoryText = "No rename history yet.";
        }
    }

    // ------------------------------------------------------------- filtering

    private bool FilterItem(object obj)
    {
        if (obj is not FileClassification item)
            return false;

        var search = SearchText?.Trim();
        if (!string.IsNullOrEmpty(search) &&
            !item.OriginalName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
            !item.NewName.Contains(search, StringComparison.OrdinalIgnoreCase))
            return false;

        return ActiveFilter switch
        {
            "Images" => item.Category == "Image",
            "Audio" => item.Category == "Audio",
            "Video" => item.Category == "Video",
            "Documents" => item.Category == "Document" || item.Category == "Archive",
            "Folders" => item.Kind == ItemKind.Folder,
            "Conflicts" => item.Tag == "conflict",
            _ => true
        };
    }

    private void ApplySorting()
    {
        if (_itemsView == null) return;
        ApplySortDescriptions();
    }

    private void ApplySortDescriptions()
    {
        var view = ItemsView;
        view.SortDescriptions.Clear();
        SortDescription desc = SelectedSortOrder switch
        {
            SortOrder.Category => new SortDescription(nameof(FileClassification.Category), ListSortDirection.Ascending),
            SortOrder.Purpose => new SortDescription(nameof(FileClassification.Purpose), ListSortDirection.Ascending),
            SortOrder.NewName => new SortDescription(nameof(FileClassification.NewName), ListSortDirection.Ascending),
            _ => new SortDescription(nameof(FileClassification.OriginalName), ListSortDirection.Ascending)
        };
        view.SortDescriptions.Add(desc);
    }

    private void SetAllSelection(bool selected)
    {
        foreach (var item in ItemsView.Cast<FileClassification>())
            item.IsSelected = selected;

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(ChangesCount));
        RecalculateCounts();
    }

    /// <summary>
    /// Recalculates dock counters. Collision detection requires planning,
    /// which is cheap (no disk writes).
    /// </summary>
    private void RecalculateCounts()
    {
        var plan = _planner.Plan(Items.Where(i => i.IsSelected));
        CollisionCount = plan.CollisionCount + plan.CycleCount;

        foreach (var item in Items)
            item.Tag = null;
        foreach (var entry in plan.Entries.Where(e => e.Status is RenamePlanStatus.Collision or RenamePlanStatus.Cycle))
            entry.Item.Tag = "conflict";

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(ChangesCount));
    }

    // ------------------------------------------------------------- helpers

    private void Persist()
    {
        _settings.LastFolder = SelectedFolderPath;
        _settings.RecursiveScan = IsRecursiveScan;
        _settings.IncludeHiddenFiles = IncludeHiddenFiles;
        _settings.IncludeSystemFiles = IncludeSystemFiles;
        _settings.PreserveExtension = PreserveExtension;
        _settings.NamingStyle = SelectedNamingStyle;
        _settings.SortOrder = SelectedSortOrder;
        _settings.Save();
    }

    private void Log(string message)
    {
        LogOutput += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
    }

    private static void OnDispatcher(Action action)
    {
        var app = Application.Current;
        if (app == null) { action(); return; }
        var dispatcher = app.Dispatcher;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
