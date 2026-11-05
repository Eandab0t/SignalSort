using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartFileClassifier.Models;

namespace SmartFileClassifier.Core;

/// <summary>
/// Classifies files and folders into categories and purposes based on metadata and content.
/// Heuristics are deterministic and offline: media duration, bitrate, channel count,
/// resolution and file size - never the file extension alone.
/// </summary>
public class FileClassifier
{
    private readonly MetadataReader _metadataReader;

    // Audio file extensions
    private static readonly HashSet<string> AudioExtensions = new()
    {
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma", ".aiff", ".aif", ".opus"
    };

    // Video file extensions
    private static readonly HashSet<string> VideoExtensions = new()
    {
        ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg"
    };

    // Image file extensions
    private static readonly HashSet<string> ImageExtensions = new()
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".ico", ".svg"
    };

    // Archive file extensions
    private static readonly HashSet<string> ArchiveExtensions = new()
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".iso"
    };

    public FileClassifier(MetadataReader metadataReader)
    {
        _metadataReader = metadataReader;
    }

    public static bool IsAudio(string extension) => AudioExtensions.Contains(Normalize(extension));
    public static bool IsVideo(string extension) => VideoExtensions.Contains(Normalize(extension));
    public static bool IsImage(string extension) => ImageExtensions.Contains(Normalize(extension));
    public static bool IsArchive(string extension) => ArchiveExtensions.Contains(Normalize(extension));
    public static bool IsMedia(string extension) => IsAudio(extension) || IsVideo(extension);

    private static string Normalize(string extension) =>
        (extension ?? string.Empty).ToLowerInvariant();

    /// <summary>
    /// Detects the broad category of a file based on its extension.
    /// </summary>
    public string DetectCategory(FileInfo file)
    {
        var ext = Normalize(file.Extension);

        if (IsAudio(ext)) return "Audio";
        if (IsVideo(ext)) return "Video";
        if (IsImage(ext)) return "Image";
        if (IsArchive(ext)) return "Archive";
        return "Document";
    }

    /// <summary>
    /// Classifies an audio file based on duration, bitrate, and channel data.
    /// </summary>
    public string ClassifyAudio(FileInfo file, List<ClassificationSignal> signals, out ClassificationConfidence confidence)
    {
        confidence = ClassificationConfidence.Unknown;
        var metadata = _metadataReader.Read(file);

        if (metadata == null)
        {
            signals.Add(new ClassificationSignal("Metadata", "Not available"));
            return "UnknownAudio";
        }

        signals.Add(new ClassificationSignal("Channels", metadata.Channels?.ToString() ?? "unknown"));
        signals.Add(new ClassificationSignal("Bitrate", metadata.BitrateKbps != null ? $"{metadata.BitrateKbps} kbps" : "unknown"));
        if (metadata.Duration.HasValue)
            signals.Add(new ClassificationSignal("Duration", FormatDuration(metadata.Duration)));

        // VoiceMemo: Mono + low bitrate
        if (metadata.IsMono && metadata.IsLowBitrate)
        {
            confidence = ClassificationConfidence.Strong;
            return "VoiceMemo";
        }

        var duration = metadata.Duration;
        if (!duration.HasValue)
            return "UnknownAudio";

        confidence = ClassificationConfidence.Strong;
        var durationSeconds = duration.Value.TotalSeconds;

        return durationSeconds switch
        {
            < 10 => "SoundEffect",
            <= 45 => "Stinger",
            _ => "Song"
        };
    }

    /// <summary>
    /// Classifies a video file based on duration and resolution.
    /// </summary>
    public string ClassifyVideo(FileInfo file, List<ClassificationSignal> signals, out ClassificationConfidence confidence)
    {
        confidence = ClassificationConfidence.Unknown;
        var metadata = _metadataReader.Read(file);

        if (metadata == null)
        {
            signals.Add(new ClassificationSignal("Metadata", "Not available"));
            return "UnknownVideo";
        }

        if (metadata.HasResolution)
        {
            signals.Add(new ClassificationSignal("Resolution", $"{metadata.Width}x{metadata.Height}"));
            signals.Add(new ClassificationSignal("Duration", metadata.Duration.HasValue ? FormatDuration(metadata.Duration) : "unknown"));
        }

        // Resolution-based classification takes precedence
        if (metadata.IsBelow720p)
        {
            confidence = ClassificationConfidence.Strong;
            return "ScreenRec";
        }

        if (metadata.IsAtLeast1080p)
        {
            confidence = ClassificationConfidence.Strong;
            return "CameraFootage";
        }

        var duration = metadata.Duration;
        if (!duration.HasValue)
            return "UnknownVideo";

        confidence = ClassificationConfidence.Strong;
        var durationSeconds = duration.Value.TotalSeconds;

        return durationSeconds switch
        {
            < 15 => "Clip",
            <= 120 => "Short",
            _ => "Recording"
        };
    }

    /// <summary>
    /// Classifies a folder based on its contents.
    /// </summary>
    public (string Category, string Purpose, ClassificationConfidence Confidence) ClassifyFolder(DirectoryInfo folder)
    {
        if (!folder.Exists)
            return ("Folder", "UnknownFolder", ClassificationConfidence.Unknown);

        var subDirs = folder.GetDirectories();
        var files = folder.GetFiles();

        // Check for dated subfolders (Archive)
        var datedSubdirs = subDirs.Where(d =>
            DateTime.TryParseExact(d.Name, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None,
                out _) ||
            DateTime.TryParseExact(d.Name, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None,
                out _)
        ).ToList();

        if (datedSubdirs.Any())
        {
            var date = datedSubdirs.First().Name;
            return ("Folder", $"Archive_{date}", ClassificationConfidence.Strong);
        }

        // Count file types
        var audioCount = files.Count(f => IsAudio(f.Extension));
        var videoCount = files.Count(f => IsVideo(f.Extension));
        var imageCount = files.Count(f => IsImage(f.Extension));
        var totalCount = audioCount + videoCount + imageCount;

        // Determine dominant content type
        if (totalCount == 0)
            return ("Folder", "Empty", ClassificationConfidence.Unknown);

        var audioRatio = (double)audioCount / totalCount;
        var videoRatio = (double)videoCount / totalCount;
        var imageRatio = (double)imageCount / totalCount;

        // Mixed media: no single type dominates (>50%)
        if (audioRatio <= 0.5 && videoRatio <= 0.5)
            return ("Folder", "MediaDump", ClassificationConfidence.Heuristic);

        // Determine the majority type
        if (audioRatio > videoRatio && audioRatio > imageRatio)
            return ("Folder", "AudioCollection", ClassificationConfidence.Heuristic);

        if (videoRatio > imageRatio)
            return ("Folder", "VideoSet", ClassificationConfidence.Heuristic);

        return ("Folder", "ImagePack", ClassificationConfidence.Heuristic);
    }

    /// <summary>
    /// Classifies a single file, determining its category and purpose.
    /// Signals and confidence describe the evidence behind the result.
    /// </summary>
    public (string Category, string Purpose, ClassificationConfidence Confidence, DateTime? DateTaken)
        Classify(FileInfo file, List<ClassificationSignal> signals)
    {
        var category = DetectCategory(file);
        signals.Add(new ClassificationSignal("Extension", file.Extension));

        switch (category)
        {
            case "Audio":
            {
                var purpose = ClassifyAudio(file, signals, out var confidence);
                var taken = _metadataReader.Read(file)?.Duration != null ? (DateTime?)file.LastWriteTimeUtc : null;
                return (category, purpose, confidence, taken);
            }
            case "Video":
            {
                var purpose = ClassifyVideo(file, signals, out var confidence);
                var taken = file.LastWriteTimeUtc;
                return (category, purpose, confidence, taken);
            }
            case "Image":
                return ClassifyImage(file, signals);
            case "Archive":
                signals.Add(new ClassificationSignal("Contents", "Compressed archive"));
                return (category, "Archive", ClassificationConfidence.Strong, null);
            default:
                signals.Add(new ClassificationSignal("Type", "Document / other"));
                return (category, "Document", ClassificationConfidence.Heuristic, null);
        }
    }

    private (string Category, string Purpose, ClassificationConfidence Confidence, DateTime? DateTaken)
        ClassifyImage(FileInfo file, List<ClassificationSignal> signals)
    {
        var dimensions = _metadataReader.ReadImageDimensions(file, signals, out var dateTaken);

        if (dimensions.HasValue)
        {
            signals.Add(new ClassificationSignal("Dimensions", $"{dimensions.Value.Width}x{dimensions.Value.Height}"));
            if (dateTaken.HasValue)
                signals.Add(new ClassificationSignal("Date taken", dateTaken.Value.ToString("yyyy-MM-dd HH:mm")));

            // Dimension-based classification is more truthful than file size.
            var longestSide = Math.Max(dimensions.Value.Width, dimensions.Value.Height);
            if (longestSide <= 64)
                return ("Image", "Icon", ClassificationConfidence.Strong, dateTaken);
            if (longestSide <= 256)
                return ("Image", "Thumbnail", ClassificationConfidence.Strong, dateTaken);
            return ("Image", "Photo", ClassificationConfidence.Heuristic, dateTaken);
        }

        // Fall back to file size when dimensions are unavailable.
        try
        {
            var sizeKb = file.Length / 1024;
            if (sizeKb < 100)
                return ("Image", "Thumbnail", ClassificationConfidence.Heuristic, dateTaken);
            if (sizeKb < 1024)
                return ("Image", "Icon", ClassificationConfidence.Heuristic, dateTaken);
            return ("Image", "Photo", ClassificationConfidence.Heuristic, dateTaken);
        }
        catch
        {
            return ("Image", "Image", ClassificationConfidence.Unknown, dateTaken);
        }
    }

    private static string FormatDuration(TimeSpan? duration) =>
        duration == null ? "unknown" : $"{(int)duration.Value.TotalMinutes}m {duration.Value.Seconds}s";
}
