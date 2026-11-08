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
using SignalSort.Core;
using SignalSort.Infrastructure.Updates;
using SignalSort.Models;

namespace SignalSort.ViewModels;

/// <summary>
/// The main view model. Orchestrates scanning, classification preview,
/// rename planning/execution and undo. All long-running work is async with
/// progress reporting and cancellation; the UI is updated on the dispatcher.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly MetadataReader _metadataReader = new();
    private readonly FileClassifier _classifier;
    private readonly FileScanner _scanner;    private readonly NameGenerator _nameGenerator = new();
    private readonly RenamePlanner _planner = new();
    private readonly RenameJournal _journal;
    private readonly AppSettings _settings;

    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _renameCts;

    private ObservableCollection<FileClassification> _items = new();
    private ICollectionView? _itemsView;
    private int _planGeneration;
    private bool _suppressItemEvents;

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
    private string _updateManifestUrl = "";
    private string _updatesStatus = "Never checked.";
    private ObservableCollection<UpdateInfo> _availableUpdates = new();

    public MainViewModel()
    {
        _classifier = new FileClassifier(_metadataReader);
        _scanner = new FileScanner(_classifier);
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
        _updateManifestUrl = _settings.UpdateManifestUrl;

        Items = new ObservableCollection<FileClassification>();

        BrowseCommand = new RelayCommand(_ => BrowseForFolder(), _ => !IsBusy);
        ScanCommand = new RelayCommand(_ => _ = ScanAsync(), _ => !IsBusy && Directory.Exists(SelectedFolderPath));
        CancelScanCommand = new RelayCommand(_ => _scanCts?.Cancel(), _ => IsBusy && _scanCts != null);
        RenameCommand = new RelayCommand(_ => _ = RenameSelectedAsync(), _ => !IsBusy && SelectedCount > 0);
        PreviewCommand = new RelayCommand(_ => _ = PreviewPlan(), _ => !IsBusy && SelectedCount > 0);
        UndoCommand = new RelayCommand(_ => _ = UndoLastAsync(), _ => !IsBusy && CanUndo);
        ClearLogCommand = new RelayCommand(_ => LogOutput = "", _ => true);
        ToggleThemeCommand = new RelayCommand(_ => IsDarkTheme = !IsDarkTheme, _ => true);
        SelectAllCommand = new RelayCommand(p => SetAllSelection(true), _ => !IsBusy);
        SelectNoneCommand = new RelayCommand(_ => SetAllSelection(false), _ => !IsBusy);
        CheckForUpdatesCommand = new RelayCommand(_ => _ = CheckForUpdatesAsync(), _ => !IsBusy);

        CanUndo = _journal.CanUndo();
        LoadHistoryText();

        App.ApplyTheme(_isDarkTheme);
    }

    // STATE
    // Item collection and live view plumbing.

    public ObservableCollection<FileClassification> Items
    {
        get => _items;
        private set
        {
            if (_items != null)
                foreach (var item in _items)
                    item.PropertyChanged -= Item_PropertyChanged;

            _items = value;
            foreach (var item in _items)
                item.PropertyChanged += Item_PropertyChanged;

            _itemsView = CollectionViewSource.GetDefaultView(_items);
            _itemsView.Filter = FilterItem;
            _itemsView.SortDescriptions.Clear();
            ApplySortDescriptions();
            OnPropertyChanged();
        }
    }

    public ICollectionView ItemsView => _itemsView ?? CollectionViewSource.GetDefaultView(Items);

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressItemEvents)
            return;

        if (e.PropertyName is nameof(FileClassification.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(ChangesCount));
            BumpPlanGeneration();
            _ = PlanInBackground();
        }
        else if (e.PropertyName is nameof(FileClassification.NewName))
        {
            OnPropertyChanged(nameof(ChangesCount));
        }
    }

    // PROPS
    // Bindable state.

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
        set { _searchText = value; OnPropertyChanged(); ItemsView.Refresh(); }
    }

    public string ActiveFilter
    {
        get => _activeFilter;
        set { _activeFilter = value; OnPropertyChanged(); ItemsView.Refresh(); }
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

    /// <summary>User-configured HTTPS manifest URL for update checks. Empty disables checks.</summary>
    public string UpdateManifestUrl
    {
        get => _updateManifestUrl;
        set { _updateManifestUrl = value; OnPropertyChanged(); Persist(); }
    }

    /// <summary>Status line for the Updates section in Settings.</summary>
    public string UpdatesStatus
    {
        get => _updatesStatus;
        set { _updatesStatus = value; OnPropertyChanged(); }
    }

    /// <summary>Updates listed by the last successful check. Empty when up to date.</summary>
    public ObservableCollection<UpdateInfo> AvailableUpdates
    {
        get => _availableUpdates;
        private set { _availableUpdates = value; OnPropertyChanged(); }
    }

    /// <summary>The running application version, e.g. "0.3.0".</summary>
    public string CurrentVersion
    {
        get
        {
            var v = typeof(MainViewModel).Assembly.GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    // DERIVED
    // Computed bindables.

    /// <summary>
    /// Naming styles exposed to the toolbar ComboBox as friendly-label pairs.
    /// </summary>
    public IReadOnlyList<KeyValuePair<NamingStyle, string>> NamingStyles =>
        Enum.GetValues<NamingStyle>().Select(s => new KeyValuePair<NamingStyle, string>(s, s.GetDisplayName())).ToList();

    /// <summary>
    /// Sort orders exposed to the toolbar ComboBox as friendly-label pairs.
    /// </summary>
    public IReadOnlyList<KeyValuePair<SortOrder, string>> SortOrders =>
        Enum.GetValues<SortOrder>().Select(s => new KeyValuePair<SortOrder, string>(s, s.GetDisplayName())).ToList();

    public int SelectedCount => Items.Count(i => i.IsSelected);
    public int ChangesCount => Items.Count(i => i.IsSelected &&
        !string.Equals(i.OriginalName, i.NewName, StringComparison.OrdinalIgnoreCase));

    public bool HasFiles => Items.Count > 0;

    /// <summary>
    /// Cancels any in-flight scan or rename. Renames stop at the next entry
    /// boundary; everything completed before that point is journaled.
    /// </summary>
    public void RequestShutdown()
    {
        _scanCts?.Cancel();
        _renameCts?.Cancel();
    }

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


    // COMMANDS
    // Relay commands bound in XAML.

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
    public RelayCommand CheckForUpdatesCommand { get; }

    // SCAN
    // Scan orchestration, progress, cancellation.

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
        if (IsBusy || string.IsNullOrWhiteSpace(SelectedFolderPath) || !Directory.Exists(SelectedFolderPath))
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

    // NAME
    // Name generation and regeneration.

    /// <summary>
    /// Regenerates all names, then re-plans in the background.
    /// </summary>
    private void RegenerateNames()
    {
        _nameGenerator.AssignNames(Items, SelectedNamingStyle, PreserveExtension);
        BumpPlanGeneration();
        ItemsView.Refresh();
        OnPropertyChanged(nameof(ChangesCount));
        _ = PlanInBackground();
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
        if (!item.Signals.Any(s => s.Label == "Corrected by user"))
            item.Signals.Add(new ClassificationSignal("Corrected by user", item.Purpose));

        RegenerateNames();
        Log($"Purpose corrected: {item.OriginalName} -> {item.Purpose}");
    }

    private async Task PreviewPlan()
    {
        var plan = await PlanSelectedAsync();
        CollisionCount = plan.CollisionCount + plan.CycleCount;
        ApplyPlan(plan);

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

    // RENAME
    // Execution and undo.

    private async Task RenameSelectedAsync()
    {
        var plan = await PlanSelectedAsync();
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

            // On cancellation the journal is already durable at the stop
            // point; a full rescan would delay shutdown, so skip it.
            if (result.Succeeded > 0 && !result.WasCancelled)
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

    // UPD
    // Update discovery and status.

    /// <summary>
    /// Checks the configured manifest URL for updates. Never runs while a
    /// scan or rename is active (the command is also disabled when busy):
    /// update work is strictly serialized with file operations, and applying
    /// anything remains a separate, user-confirmed step. See UPDATE_SYSTEM.md.
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(UpdateManifestUrl))
        {
            UpdatesStatus = "Updates are disabled - no manifest URL configured.";
            return;
        }

        if (!Uri.TryCreate(UpdateManifestUrl, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps)
        {
            UpdatesStatus = "Update check failed: the manifest URL must be an HTTPS address.";
            return;
        }

        IsBusy = true;
        UpdatesStatus = "Checking for updates...";
        try
        {
            var source = new HttpsManifestSource(url);
            var manager = new UpdateManager(source, new UnverifiedSigner(), CurrentVersion);
            var result = await manager.CheckForUpdatesAsync();

            AvailableUpdates = new ObservableCollection<UpdateInfo>(result.Updates);
            UpdatesStatus = !result.ManifestResult.IsValid
                ? $"Update check failed: {result.ManifestResult.Reason}"
                : result.IsAvailable
                    ? $"{result.Updates.Count} update(s) available. Review them below - nothing is applied automatically."
                    : "You are up to date.";
        }
        catch (Exception ex)
        {
            AvailableUpdates = new ObservableCollection<UpdateInfo>();
            UpdatesStatus = $"Update check failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // HISTORY
    // Recent batch rendering.

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

    // FILTER
    // Search and category filters.

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
        _suppressItemEvents = true;
        try
        {
            foreach (var item in ItemsView.Cast<FileClassification>())
                item.IsSelected = selected;
        }
        finally
        {
            _suppressItemEvents = false;
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(ChangesCount));
        BumpPlanGeneration();
        _ = PlanInBackground();
    }

    // PLAN
    // Background planning with stale-result rejection.

    private void BumpPlanGeneration() => _planGeneration++;

    private static FileClassification CloneForPlanning(FileClassification item) => new()
    {
        FullPath = item.FullPath,
        OriginalName = item.OriginalName,
        NewName = item.NewName,
        Extension = item.Extension,
        Kind = item.Kind,
    };

    /// <summary>
    /// Plans the selected items on a background thread and applies the result
    /// to the UI (collision tags, de-conflicted names, dock counters).
    /// Discards the result when the underlying state changed meanwhile.
    /// </summary>
    private async Task PlanInBackground()
    {
        int generation = _planGeneration;
        var snapshot = Items.Where(i => i.IsSelected).Select(CloneForPlanning).ToList();
        var plan = await Task.Run(() => _planner.Plan(snapshot));
        if (generation != _planGeneration)
            return;
        ApplyPlan(plan);
    }

    /// <summary>
    /// Plans for an immediate operation (preview/rename), retrying if the
    /// underlying state changed while planning ran in the background.
    /// </summary>
    private async Task<RenamePlan> PlanSelectedAsync()
    {
        while (true)
        {
            int generation = _planGeneration;
            var snapshot = Items.Where(i => i.IsSelected).Select(CloneForPlanning).ToList();
            var plan = await Task.Run(() => _planner.Plan(snapshot));
            if (generation == _planGeneration)
                return plan;
        }
    }

    private void ApplyPlan(RenamePlan plan)
    {
        var byOldPath = Items.ToDictionary(i => i.FullPath, StringComparer.OrdinalIgnoreCase);

        foreach (var item in Items)
            item.Tag = null;

        foreach (var entry in plan.Entries)
        {
            if (!byOldPath.TryGetValue(entry.OldPath, out var real))
                continue;

            real.NewName = Path.GetFileName(entry.NewPath);

            if (entry.Status is RenamePlanStatus.Collision or RenamePlanStatus.Cycle)
                real.Tag = "conflict";
        }

        CollisionCount = plan.CollisionCount + plan.CycleCount;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(ChangesCount));
        ItemsView.Refresh();
    }

    // HELPERS
    // Persistence, logging, dispatcher.

    private void Persist()
    {
        _settings.LastFolder = SelectedFolderPath;
        _settings.RecursiveScan = IsRecursiveScan;
        _settings.IncludeHiddenFiles = IncludeHiddenFiles;
        _settings.IncludeSystemFiles = IncludeSystemFiles;
        _settings.PreserveExtension = PreserveExtension;
        _settings.NamingStyle = SelectedNamingStyle;
        _settings.SortOrder = SelectedSortOrder;
        _settings.UpdateManifestUrl = UpdateManifestUrl;
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
