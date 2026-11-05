using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SignalSort.Models;

/// <summary>
/// Whether the item is a file or a directory.
/// </summary>
public enum ItemKind
{
    File,
    Folder
}

/// <summary>
/// How sure the classifier is about a detected purpose.
/// Strong = derived from media metadata; Heuristic = derived from
/// indirect signals such as file size; Unknown = no evidence found.
/// </summary>
public enum ClassificationConfidence
{
    Strong,
    Heuristic,
    Unknown
}

/// <summary>
/// One piece of evidence behind a classification, shown in the inspector
/// (e.g. "Channels: 1", "Resolution: 640x360").
/// </summary>
public class ClassificationSignal
{
    public ClassificationSignal(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public string Value { get; }
}

/// <summary>
/// Represents metadata extracted from an audio or video file.
/// </summary>
public class MediaMetadata
{
    public TimeSpan? Duration { get; set; }
    public int? BitrateKbps { get; set; }
    public int? SampleRateHz { get; set; }
    public int? Channels { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public bool IsMono => Channels.HasValue && Channels <= 1;
    public bool IsLowBitrate => BitrateKbps.HasValue && BitrateKbps < 128;
    public int? ResolutionHeight => Height;
    public bool IsBelow720p => Height.HasValue && Height < 720;
    public bool IsAtLeast1080p => Height.HasValue && Height >= 1080;

    public bool HasDuration => Duration.HasValue;
    public bool HasResolution => Width.HasValue && Height.HasValue;
}

/// <summary>
/// Represents the classification result for a single file or folder.
/// Implements INotifyPropertyChanged for WPF data binding.
/// </summary>
public class FileClassification : INotifyPropertyChanged
{
    private string _originalName = string.Empty;
    private string _category = string.Empty;
    private string _purpose = string.Empty;
    private string _newName = string.Empty;
    private bool _isSelected = true;

    /// <summary>
    /// The original file or folder name.
    /// </summary>
    public string OriginalName
    {
        get => _originalName;
        set { _originalName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Whether this item is a file or a folder.
    /// </summary>
    public ItemKind Kind { get; set; } = ItemKind.File;

    /// <summary>
    /// How confident the classifier is about <see cref="Purpose"/>.
    /// </summary>
    public ClassificationConfidence Confidence { get; set; } = ClassificationConfidence.Unknown;

    /// <summary>
    /// Evidence shown in the inspector (channels, bitrate, resolution, ...).
    /// </summary>
    public List<ClassificationSignal> Signals { get; set; } = new();

    /// <summary>
    /// Best-known capture date: EXIF date taken for images, media creation
    /// time for audio/video, falling back to the file system dates.
    /// Used by the category-prefixed naming style.
    /// </summary>
    public DateTime? DateTaken { get; set; }

    /// <summary>
    /// Free-form marker for the UI (e.g. "conflict" when a generated name
    /// collides). Not part of classification.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Whether this row is the one shown in the inspector.
    /// </summary>
    private bool _isSelectedForInspect;
    public bool IsSelectedForInspect
    {
        get => _isSelectedForInspect;
        set { _isSelectedForInspect = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Compact signal line for the row (skips the trivial extension entry).
    /// </summary>
    public string SignalSummary
    {
        get
        {
            var parts = Signals
                .Where(s => !string.Equals(s.Label, "Extension", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Value)
                .Take(3);
            var summary = string.Join("  ·  ", parts);
            return summary.Length == 0 ? Extension : summary;
        }
    }

    /// <summary>
    /// Human-readable confidence, deliberately honest about uncertainty.
    /// </summary>
    public string ConfidenceLabel => Confidence switch
    {
        ClassificationConfidence.Strong => "strong match",
        ClassificationConfidence.Heuristic => "heuristic",
        _ => "uncertain"
    };

    /// <summary>
    /// The detected category (e.g., "Audio", "Video", "Folder").
    /// </summary>
    public string Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// The detected purpose within the category (e.g., "SoundEffect", "Song").
    /// </summary>
    public string Purpose
    {
        get => _purpose;
        set { _purpose = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// The generated new name based on the naming strategy.
    /// </summary>
    public string NewName
    {
        get => _newName;
        set { _newName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// The file extension preserved during renaming.
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the file.
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether this item is selected for renaming.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// The detected media metadata (for audio/video files).
    /// </summary>
    public MediaMetadata? Metadata { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Enum representing available naming styles.
/// </summary>
public enum NamingStyle
{
    /// <summary>Default: Purpose_20261104_001 — sortable, searchable, collision-safe.</summary>
    CategoryPrefixed,
    CategoryNumber,
    CategoryHash,
    CategoryTimestamp,
    CategoryAdjectiveNoun
}

/// <summary>
/// Extension methods for NamingStyle.
/// </summary>
public static class NamingStyleExtensions
{
    public static string GetDisplayName(this NamingStyle style)
    {
        return style switch
        {
            NamingStyle.CategoryPrefixed => "Category + Date + Number (default)",
            NamingStyle.CategoryNumber => "Category + Number",
            NamingStyle.CategoryHash => "Category + Hash",
            NamingStyle.CategoryTimestamp => "Category + Timestamp",
            NamingStyle.CategoryAdjectiveNoun => "Category + AdjectiveNoun",
            _ => style.ToString()
        };
    }
}
