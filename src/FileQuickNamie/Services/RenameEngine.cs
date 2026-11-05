using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartFileClassifier.Models;

namespace SmartFileClassifier.Services;

/// <summary>
/// Renames files based on classification results.
/// Logs all operations for user feedback.
/// </summary>
public class RenameEngine
{
    public Action<string>? OnLog;

    /// <summary>
    /// Renames all selected files in the classifications.
    /// </summary>
    public RenameResult RenameAll(IEnumerable<FileClassification> classifications)
    {
        var result = new RenameResult();
        var usedNames = new HashSet<string>();

        foreach (var item in classifications.Where(c => c.IsSelected))
        {
            if (string.Equals(Path.GetFileName(item.FullPath), item.NewName, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var directory = Path.GetDirectoryName(item.FullPath) ?? string.Empty;
                var newName = item.NewName;

                // Handle name collisions
                var baseName = Path.GetFileNameWithoutExtension(newName);
                var extension = Path.GetExtension(newName);
                var finalName = newName;
                var counter = 1;

                while (usedNames.Contains(finalName) ||
                       (File.Exists(Path.Combine(directory, finalName)) &&
                        !string.Equals(Path.Combine(directory, finalName), item.FullPath, StringComparison.OrdinalIgnoreCase)))
                {
                    finalName = $"{baseName}_{counter}{extension}";
                    counter++;
                }

                usedNames.Add(finalName);

                var oldPath = item.FullPath;
                var newPath = Path.Combine(directory, finalName);

                File.Move(oldPath, newPath);

                Log($"Renamed: {item.OriginalName} -> {finalName}");
                result.SuccessfulRenames++;
            }
            catch (Exception ex)
            {
                Log($"ERROR renaming {item.OriginalName}: {ex.Message}");
                result.FailedRenames++;
            }
        }

        Log($"--- Operation complete: {result.SuccessfulRenames} succeeded, {result.FailedRenames} failed ---");
        return result;
    }

    private void Log(string message)
    {
        OnLog?.Invoke(message);
        Console.WriteLine($"[RenameEngine] {message}");
    }
}

/// <summary>
/// Result of a rename operation.
/// </summary>
public class RenameResult
{
    public int SuccessfulRenames { get; set; }
    public int FailedRenames { get; set; }
    public bool IsSuccess => FailedRenames == 0;
}
