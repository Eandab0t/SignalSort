using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SmartFileClassifier.Models;

namespace SmartFileClassifier.Core;
/// <summary>
/// Persistent rename journal stored under %LOCALAPPDATA%\FileQuickNamie.
///
/// Safety properties:
///   - Entries are appended as renames succeed, so a crash can never lose the
///     record of a rename that already happened.
///   - The journal file itself is written atomically (temp + replace).
///   - Undo verifies the file is still the one we moved (size + timestamp
///     fingerprint) and that the original spot is free before reversing.
/// </summary>
public class RenameJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _journalPath;

    public RenameJournal(string? journalPath = null)
    {
        _journalPath = journalPath ?? DefaultJournalPath();
    }

    public static string DefaultJournalPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileQuickNamie");
        return Path.Combine(dir, "journal.json");
    }

    /// <summary>
    /// Loads the journal, or returns an empty document when absent/corrupt.
    /// A corrupt journal is backed up rather than overwritten so nothing is lost.
    /// </summary>
    public JournalDocument Load()
    {
        try
        {
            if (!File.Exists(_journalPath))
                return new JournalDocument();

            var json = File.ReadAllText(_journalPath);
            return JsonSerializer.Deserialize<JournalDocument>(json) ?? new JournalDocument();
        }
        catch (Exception)
        {
            TryBackupCorruptFile();
            return new JournalDocument();
        }
    }

    /// <summary>
    /// Starts a new batch and persists it immediately with status InProgress.
    /// </summary>
    public JournalBatch StartBatch(string folder)
    {
        var doc = Load();
        var batch = new JournalBatch { Folder = folder };
        doc.Batches.Add(batch);
        Save(doc);
        return batch;
    }

    /// <summary>
    /// Records one successful rename inside the batch and persists immediately.
    /// Failed renames are not journaled (nothing to reverse).
    /// </summary>
    public void RecordSuccess(JournalBatch batch, string originalPath, string newPath, FileInfo file)
    {
        var entry = new JournalEntry
        {
            OriginalPath = originalPath,
            NewPath = newPath,
            Succeeded = true
        };

        try
        {
            if (file.Exists)
            {
                entry.FileLength = file.Length;
                entry.LastWriteUtcTicks = file.LastWriteTimeUtc.Ticks;
            }
        }
        catch
        {
            // Fingerprints are best-effort; undo still works without them.
        }

        var doc = Load();
        var stored = doc.Batches.FirstOrDefault(b => b.Id == batch.Id);
        if (stored == null)
        {
            stored = batch;
            doc.Batches.Add(stored);
        }

        stored.Entries.Add(entry);
        Save(doc);
    }

    /// <summary>
    /// Marks a batch as completed or failed and persists.
    /// </summary>
    public void CompleteBatch(JournalBatch batch, JournalBatchStatus status)
    {
        var doc = Load();
        var stored = doc.Batches.FirstOrDefault(b => b.Id == batch.Id);
        if (stored == null)
        {
            stored = batch;
            doc.Batches.Add(stored);
        }

        stored.Status = status;
        stored.CompletedUtc = DateTime.UtcNow;
        Save(doc);
    }

    /// <summary>
    /// Reverses the most recent completed batch. Returns a per-entry report.
    /// Entries whose file changed after the rename, or whose original spot is
    /// now occupied, are skipped with a note instead of being forced.
    /// </summary>
    public UndoResult UndoLast(out JournalBatch? undoneBatch)
    {
        undoneBatch = null;
        var doc = Load();

        var batch = doc.Batches.LastOrDefault(b => b.Status == JournalBatchStatus.Completed);
        if (batch == null)
            return UndoResult.NotFound();

        undoneBatch = batch;
        var result = new UndoResult { BatchId = batch.Id };

        // Reverse in reverse order of execution.
        foreach (var entry in batch.Entries.AsEnumerable().Reverse())
        {
            if (!entry.Succeeded)
                continue;

            try
            {
                if (!File.Exists(entry.NewPath) && !Directory.Exists(entry.NewPath))
                {
                    result.Skipped++;
                    result.Notes.Add($"Skipped: moved file no longer exists ({Path.GetFileName(entry.NewPath)}).");
                    continue;
                }

                if (IsModified(entry))
                {
                    result.Skipped++;
                    result.Notes.Add($"Skipped: file was modified after the rename ({Path.GetFileName(entry.NewPath)}).");
                    continue;
                }

                if (File.Exists(entry.OriginalPath) || Directory.Exists(entry.OriginalPath))
                {
                    result.Skipped++;
                    result.Notes.Add($"Skipped: original location is now occupied ({Path.GetFileName(entry.OriginalPath)}).");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalPath)!);
                File.Move(entry.NewPath, entry.OriginalPath);
                result.Restored++;
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Notes.Add($"Failed: {Path.GetFileName(entry.NewPath)} - {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// True when the file at NewPath no longer matches the fingerprint recorded
    /// at rename time, meaning the user edited it afterwards and undoing the
    /// rename could confuse them.
    /// </summary>
    private bool IsModified(JournalEntry entry)
    {
        if (entry.FileLength == 0 && entry.LastWriteUtcTicks == 0)
            return false; // No fingerprint recorded - assume unmodified.

        try
        {
            var info = new FileInfo(entry.NewPath);
            if (!info.Exists)
                return true;
            return info.Length != entry.FileLength ||
                   info.LastWriteTimeUtc.Ticks != entry.LastWriteUtcTicks;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// True when at least one completed, non-empty batch exists.
    /// </summary>
    public bool CanUndo()
    {
        var doc = Load();
        return doc.Batches.Any(b => b.Status == JournalBatchStatus.Completed && b.Entries.Any(e => e.Succeeded));
    }

    /// <summary>
    /// Most recent batches, newest first, for the history view.
    /// </summary>
    public List<JournalBatch> RecentBatches(int count)
    {
        var doc = Load();
        return doc.Batches
            .OrderByDescending(b => b.StartedUtc)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Atomically persists the document, keeping the most recent batches only.
    /// </summary>
    private void Save(JournalDocument doc)
    {
        // Keep the journal bounded: newest 50 batches.
        if (doc.Batches.Count > 50)
        {
            doc.Batches = doc.Batches
                .OrderByDescending(b => b.StartedUtc)
                .Take(50)
                .OrderBy(b => b.StartedUtc)
                .ToList();
        }

        var dir = Path.GetDirectoryName(_journalPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = _journalPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(doc, JsonOptions));

        if (File.Exists(_journalPath))
            File.Replace(tmp, _journalPath, null);
        else
            File.Move(tmp, _journalPath);
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            if (File.Exists(_journalPath))
                File.Copy(_journalPath, _journalPath + ".corrupt", overwrite: true);
        }
        catch
        {
            // Nothing more we can do.
        }
    }
}

/// <summary>
/// Root document of the journal file.
/// </summary>
public class JournalDocument
{
    public List<JournalBatch> Batches { get; set; } = new();
}

/// <summary>
/// Outcome of an undo operation.
/// </summary>
public class UndoResult
{
    public string BatchId { get; set; } = "";
    public int Restored { get; set; }
    public int Skipped { get; set; }
    public List<string> Notes { get; set; } = new();

    public bool IsSuccess => Restored > 0 && Skipped == 0;

    public static UndoResult NotFound() => new()
    {
        BatchId = "",
        Notes = { "No completed rename batch was found to undo." }
    };
}
