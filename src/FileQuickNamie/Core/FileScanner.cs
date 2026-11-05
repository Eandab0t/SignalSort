using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SmartFileClassifier.Models;

namespace SmartFileClassifier.Core;

/// <summary>
/// Progress report emitted while scanning a folder.
/// </summary>
public class ScanProgress
{
    public int Processed { get; init; }
    public int Total { get; init; }
    public string CurrentName { get; init; } = "";

    public double Percent => Total == 0 ? 0 : Math.Min(100.0, Processed * 100.0 / Total);
}

/// <summary>
/// Scans a folder and classifies every file and (optionally relevant) folder.
/// All heavy work happens on a background thread with progress reporting and
/// cancellation support. Results are returned as a plain list; the caller
/// binds them to the UI on the dispatcher.
/// </summary>
public class FileScanner
{
    private readonly FileClassifier _classifier;
    private readonly MetadataReader _metadataReader;

    public FileScanner(FileClassifier classifier, MetadataReader metadataReader)
    {
        _classifier = classifier;
        _metadataReader = metadataReader;
    }

    /// <summary>
    /// Scans a folder (top-level only, or recursively) and returns classified
    /// items sorted by original name. Never throws for unreadable content;
    /// unreadable items are skipped.
    /// </summary>
    public List<FileClassification> Scan(
        string folderPath,
        bool recursive,
        bool includeHidden,
        bool includeSystem,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<FileClassification>();

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return results;

        var root = new DirectoryInfo(folderPath);

        // Classify the selected folder itself so it can be renamed too.
        results.Add(BuildFolderClassification(root));

        IEnumerable<FileSystemInfo> entries = recursive
            ? root.EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
            : root.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly);

        var materialized = entries
            .Where(f => PassesFilters(f, includeHidden, includeSystem))
            .OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = materialized.Count;
        var processed = 0;

        foreach (var entry in materialized)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (entry is DirectoryInfo dir)
                {
                    // Skip the root itself (already added) - only subfolders.
                    if (!string.Equals(dir.FullName, root.FullName, StringComparison.OrdinalIgnoreCase))
                        results.Add(BuildFolderClassification(dir));
                }
                else if (entry is FileInfo file)
                {
                    results.Add(BuildFileClassification(file));
                }
            }
            catch (Exception)
            {
                // A single unreadable entry must never abort the whole scan.
            }

            processed++;
            progress?.Report(new ScanProgress
            {
                Processed = processed,
                Total = total,
                CurrentName = entry.Name
            });
        }

        return results
            .OrderBy(f => f.OriginalName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool PassesFilters(FileSystemInfo info, bool includeHidden, bool includeSystem)
    {
        try
        {
            var attributes = info.Attributes;

            if (!includeHidden && attributes.HasFlag(FileAttributes.Hidden))
                return false;
            if (!includeSystem && attributes.HasFlag(FileAttributes.System))
                return false;
            if (info.Name.StartsWith("."))
                return false;

            // Skip reparse points (symlinks/junctions) to avoid cycles when recursive.
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                return false;

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private FileClassification BuildFileClassification(FileInfo file)
    {
        var signals = new List<ClassificationSignal>();
        var (category, purpose, confidence, dateTaken) = _classifier.Classify(file, signals);

        // Read full metadata for media files so the inspector can show it.
        MediaMetadata? metadata = null;
        if (FileClassifier.IsMedia(file.Extension))
            metadata = _metadataReader.Read(file);

        return new FileClassification
        {
            OriginalName = file.Name,
            Category = category,
            Purpose = purpose,
            FullPath = file.FullName,
            Extension = file.Extension,
            IsSelected = true,
            Kind = ItemKind.File,
            Metadata = metadata,
            DateTaken = dateTaken,
            Confidence = confidence,
            Signals = signals
        };
    }

    private FileClassification BuildFolderClassification(DirectoryInfo dir)
    {
        var (category, purpose, confidence) = _classifier.ClassifyFolder(dir);

        var signals = new List<ClassificationSignal>
        {
            new("Contents", "Folder"),
            new("Items", SafeChildCount(dir))
        };

        DateTime? dateTaken = null;
        try
        {
            dateTaken = dir.LastWriteTimeUtc == DateTime.MinValue ? null : dir.LastWriteTimeUtc;
        }
        catch
        {
            // Ignore unreadable timestamps.
        }

        return new FileClassification
        {
            OriginalName = dir.Name,
            Category = category,
            Purpose = purpose,
            FullPath = dir.FullName,
            Extension = string.Empty,
            IsSelected = true,
            Kind = ItemKind.Folder,
            DateTaken = dateTaken,
            Confidence = confidence,
            Signals = signals
        };
    }

    private static string SafeChildCount(DirectoryInfo dir)
    {
        try
        {
            var files = dir.GetFiles().Length;
            var dirs = dir.GetDirectories().Length;
            return $"{files} files, {dirs} folders";
        }
        catch
        {
            return "unreadable";
        }
    }
}
