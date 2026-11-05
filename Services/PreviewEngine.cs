using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SmartFileClassifier.Models;
using SmartFileClassifier.Naming;

namespace SmartFileClassifier.Services;

/// <summary>
/// Scans a folder and generates file classifications with new names.
/// </summary>
public class PreviewEngine
{
    private readonly FileClassifier _classifier;
    private readonly NamingStyle _currentStyle;

    public PreviewEngine(FileClassifier classifier, NamingStyle currentStyle)
    {
        _classifier = classifier;
        _currentStyle = currentStyle;
    }

    /// <summary>
    /// Scans the specified folder and returns classifications for all files.
    /// </summary>
    public async Task<ObservableCollection<FileClassification>> ScanFolderAsync(
        string folderPath,
        bool includeHidden = false,
        bool includeSystem = false)
    {
        var results = new ObservableCollection<FileClassification>();

        if (!Directory.Exists(folderPath))
            return results;

        await Task.Run(() =>
        {
            try
            {
                var dir = new DirectoryInfo(folderPath);
                var files = dir.GetFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(f => (!f.Attributes.HasFlag(FileAttributes.Hidden) || includeHidden) &&
                                (!f.Attributes.HasFlag(FileAttributes.System) || includeSystem) &&
                                !f.Name.StartsWith("."))
                    .OrderBy(f => f.Name)
                    .ToList();

                var (folderCategory, folderPurpose) = _classifier.ClassifyFolder(dir);

                var strategy = NamingStrategyFactory.Create(_currentStyle);
                var numberStrategy = _currentStyle == NamingStyle.CategoryNumber
                    ? new CategoryNumberStrategy()
                    : null;

                foreach (var file in files)
                {
                    var (category, purpose) = _classifier.Classify(file);

                    var classification = new FileClassification
                    {
                        OriginalName = file.Name,
                        Category = category,
                        Purpose = purpose,
                        FullPath = file.FullName,
                        Extension = file.Extension,
                        IsSelected = true,
                        Metadata = GetMetadata(file)
                    };

                    classification.NewName = GenerateNewName(strategy, numberStrategy, classification, true);

                    results.Add(classification);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied to folder: {folderPath} - {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning folder: {folderPath} - {ex.Message}");
            }
        });

        return results;
    }

    /// <summary>
    /// Scans a subfolder recursively and returns classifications.
    /// </summary>
    public async Task<ObservableCollection<FileClassification>> ScanFolderRecursiveAsync(
        string folderPath,
        bool includeHidden = false,
        bool includeSystem = false)
    {
        var results = new ObservableCollection<FileClassification>();

        if (!Directory.Exists(folderPath))
            return results;

        await Task.Run(() =>
        {
            try
            {
                var dir = new DirectoryInfo(folderPath);
                var allFiles = dir.GetFiles("*", SearchOption.AllDirectories)
                    .Where(f => (!f.Attributes.HasFlag(FileAttributes.Hidden) || includeHidden) &&
                                (!f.Attributes.HasFlag(FileAttributes.System) || includeSystem) &&
                                !f.Name.StartsWith("."))
                    .OrderBy(f => f.FullName)
                    .ToList();

                var strategy = NamingStrategyFactory.Create(_currentStyle);
                var numberStrategy = _currentStyle == NamingStyle.CategoryNumber
                    ? new CategoryNumberStrategy()
                    : null;

                foreach (var file in allFiles)
                {
                    var (category, purpose) = _classifier.Classify(file);

                    var classification = new FileClassification
                    {
                        OriginalName = file.Name,
                        Category = category,
                        Purpose = purpose,
                        FullPath = file.FullName,
                        Extension = file.Extension,
                        IsSelected = true
                    };

                    classification.NewName = GenerateNewName(strategy, numberStrategy, classification, true);

                    results.Add(classification);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning folder recursively: {folderPath} - {ex.Message}");
            }
        });

        return results;
    }

    private MediaMetadata? GetMetadata(FileInfo file)
    {
        try
        {
            var ext = file.Extension.ToLowerInvariant();
            var audioExts = new[] { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma" };
            var videoExts = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" };

            if (audioExts.Contains(ext) || videoExts.Contains(ext))
            {
                using var tagFile = TagLib.File.Create(file.FullName);
                if (tagFile?.Properties != null)
                {
                    return new MediaMetadata
                    {
                        Duration = tagFile.Properties.Duration,
                        BitrateKbps = (int)(tagFile.Properties.AudioBitrate / 1000),
                        SampleRateHz = tagFile.Properties.AudioSampleRate,
                        Channels = tagFile.Properties.AudioChannels,
                        Width = tagFile.Properties.VideoWidth > 0 ? tagFile.Properties.VideoWidth : null,
                        Height = tagFile.Properties.VideoHeight > 0 ? tagFile.Properties.VideoHeight : null
                    };
                }
            }
        }
        catch
        {
            // No metadata available
        }

        return null;
    }

    private string GenerateNewName(INamingStrategy strategy, CategoryNumberStrategy? numberStrategy,
        FileClassification classification, bool preserveExtension)
    {
        var fileInfo = new FileInfo(classification.FullPath);
        string baseName;

        if (_currentStyle == NamingStyle.CategoryNumber && numberStrategy != null)
        {
            baseName = numberStrategy.GenerateName(classification.Purpose, fileInfo);
        }
        else
        {
            baseName = strategy.GenerateName(classification.Purpose, fileInfo);
        }

        if (preserveExtension)
        {
            var extension = classification.Extension ?? string.Empty;
            return $"{baseName}{extension}";
        }

        return baseName;
    }

    /// <summary>
    /// Regenerates new names for all classifications using the specified naming style.
    /// </summary>
    public void RegenerateNames(ObservableCollection<FileClassification> classifications,
        NamingStyle newStyle, bool preserveExtension = true)
    {
        var strategy = NamingStrategyFactory.Create(newStyle);
        var numberStrategy = newStyle == NamingStyle.CategoryNumber
            ? new CategoryNumberStrategy()
            : null;

        foreach (var classification in classifications)
        {
            var fileInfo = new FileInfo(classification.FullPath);
            classification.NewName = GenerateNewName(strategy, numberStrategy, classification, preserveExtension);
        }
    }
}
