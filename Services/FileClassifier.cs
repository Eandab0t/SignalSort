using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartFileClassifier.Models;

namespace SmartFileClassifier.Services;

/// <summary>
/// Classifies files and folders into categories and purposes based on metadata and content.
/// </summary>
public class FileClassifier
{
    private readonly MetadataExtractor _metadataExtractor;

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

    public FileClassifier(MetadataExtractor metadataExtractor)
    {
        _metadataExtractor = metadataExtractor;
    }

    /// <summary>
    /// Detects the broad category of a file based on its extension.
    /// </summary>
    public string DetectCategory(FileInfo file)
    {
        var ext = file.Extension.ToLowerInvariant();

        return ext switch
        {
            var e when AudioExtensions.Contains(e) => "Audio",
            var e when VideoExtensions.Contains(e) => "Video",
            var e when ImageExtensions.Contains(e) => "Image",
            var e when ArchiveExtensions.Contains(e) => "Archive",
            _ => "Document"
        };
    }

    /// <summary>
    /// Classifies an audio file based on duration, bitrate, and channel data.
    /// </summary>
    public string ClassifyAudio(FileInfo file)
    {
        var metadata = _metadataExtractor.Extract(file);

        if (metadata == null)
            return "UnknownAudio";

        // VoiceMemo: Mono + low bitrate
        if (metadata.IsMono && metadata.IsLowBitrate)
            return "VoiceMemo";

        var duration = metadata.Duration;
        if (!duration.HasValue)
            return "UnknownAudio";

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
    public string ClassifyVideo(FileInfo file)
    {
        var metadata = _metadataExtractor.Extract(file);

        if (metadata == null)
            return "UnknownVideo";

        // Resolution-based classification takes precedence
        if (metadata.IsBelow720p)
            return "ScreenRec";

        if (metadata.IsAtLeast1080p)
            return "CameraFootage";

        var duration = metadata.Duration;
        if (!duration.HasValue)
            return "UnknownVideo";

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
    public (string Category, string Purpose) ClassifyFolder(DirectoryInfo folder)
    {
        if (!folder.Exists)
            return ("Folder", "UnknownFolder");

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
            return ("Folder", $"Archive_{date}");
        }

        // Count file types
        var audioCount = files.Count(f => AudioExtensions.Contains(f.Extension.ToLowerInvariant()));
        var videoCount = files.Count(f => VideoExtensions.Contains(f.Extension.ToLowerInvariant()));
        var imageCount = files.Count(f => ImageExtensions.Contains(f.Extension.ToLowerInvariant()));
        var totalCount = audioCount + videoCount + imageCount;

        // Determine dominant content type
        if (totalCount == 0)
            return ("Folder", "Empty");

        var audioRatio = (double)audioCount / totalCount;
        var videoRatio = (double)videoCount / totalCount;
        var imageRatio = (double)imageCount / totalCount;

        // Mixed media: no single type dominates (>50%)
        if (audioRatio <= 0.5 && videoRatio <= 0.5)
        {
            return ("Folder", "MediaDump");
        }

        // Determine the majority type
        if (audioRatio > videoRatio && audioRatio > imageRatio)
            return ("Folder", "AudioCollection");

        if (videoRatio > imageRatio)
            return ("Folder", "VideoSet");

        return ("Folder", "ImagePack");
    }

    /// <summary>
    /// Classifies a single file, determining its category and purpose.
    /// </summary>
    public (string Category, string Purpose) Classify(FileInfo file)
    {
        var category = DetectCategory(file);

        var purpose = category switch
        {
            "Audio" => ClassifyAudio(file),
            "Video" => ClassifyVideo(file),
            "Image" => ClassifyImage(file),
            "Archive" => "Archive",
            _ => "Document"
        };

        return (category, purpose);
    }

    private string ClassifyImage(FileInfo file)
    {
        // Images don't have rich metadata for sub-categorization in this app,
        // but we could add logic like: small images -> Icons, large -> Photos, etc.
        try
        {
            var fileInfo = new FileInfo(file.FullName);
            var sizeKb = fileInfo.Length / 1024;

            if (sizeKb < 100)
                return "Thumbnail";
            if (sizeKb < 1024)
                return "Icon";
            return "Photo";
        }
        catch
        {
            return "Image";
        }
    }
}
