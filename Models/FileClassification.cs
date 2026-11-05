using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartFileClassifier.Models;

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
            NamingStyle.CategoryNumber => "Category + Number",
            NamingStyle.CategoryHash => "Category + Hash",
            NamingStyle.CategoryTimestamp => "Category + Timestamp",
            NamingStyle.CategoryAdjectiveNoun => "Category + AdjectiveNoun",
            _ => style.ToString()
        };
    }
}
