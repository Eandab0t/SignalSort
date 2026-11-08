using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SignalSort.Models;

namespace SignalSort.Core;

/// <summary>
/// Progress report for a running rename batch.
/// </summary>
public class RenameProgress
{
    public int Processed { get; init; }
    public int Total { get; init; }
    public string CurrentName { get; init; } = "";

    public double Percent => Total == 0 ? 0 : Math.Min(100.0, Processed * 100.0 / Total);
}

/// <summary>
/// Outcome of one executed rename.
/// </summary>
public class RenameOutcome
{
    public string OriginalName { get; init; } = "";
    public string FinalName { get; init; } = "";
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Executes a validated <see cref="RenamePlan"/> on the disk.
///
/// Safety strategy:
///   - Only entries the planner marked Ready are executed, in the order the
///     planner produced (chain-safe).
///   - Before every move the destination is re-checked: planning-time
///     guarantees can be invalidated by external changes mid-batch.
///   - Every success is journaled immediately, so undo is always possible
///     even if the process dies halfway through.
///   - File.Move without overwrite: the runtime itself refuses to clobber.
/// </summary>
public class RenameExecutor
{
    private readonly RenameJournal _journal;

    public RenameExecutor(RenameJournal journal)
    {
        _journal = journal;
    }

    /// <summary>
    /// Executes all Ready entries of the plan.
    /// </summary>
    public async Task<RenameBatchResult> ExecuteAsync(
        RenamePlan plan,
        string folder,
        IProgress<RenameProgress>? progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var result = new RenameBatchResult();
            var ready = plan.Entries.Where(e => e.Status == RenamePlanStatus.Ready).ToList();
            var total = ready.Count;

            var batch = _journal.StartBatch(folder);
            result.JournalBatchId = batch.Id;

            var processed = 0;
            try
            {
                foreach (var entry in ready)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var outcome = ExecuteOne(entry, batch);
                    result.Outcomes.Add(outcome);
                    processed++;

                    progress?.Report(new RenameProgress
                    {
                        Processed = processed,
                        Total = total,
                        CurrentName = outcome.OriginalName
                    });

                    if (outcome.Succeeded)
                        result.Succeeded++;
                    else
                        result.Failed++;
                }

                _journal.CompleteBatch(batch,
                    result.Failed == 0 ? JournalBatchStatus.Completed : JournalBatchStatus.Failed);
                result.BatchStatus = result.Failed == 0
                    ? JournalBatchStatus.Completed
                    : JournalBatchStatus.Failed;
            }
            catch (OperationCanceledException)
            {
                _journal.CompleteBatch(batch,
                    result.Succeeded > 0 ? JournalBatchStatus.Completed : JournalBatchStatus.Failed);
                result.BatchStatus = JournalBatchStatus.Failed;
                result.WasCancelled = true;
                result.Notes.Add($"Cancelled after {result.Succeeded} of {total} renames. " +
                                 "The completed renames can be undone.");
            }
            catch (Exception ex)
            {
                _journal.CompleteBatch(batch, JournalBatchStatus.Failed);
                result.BatchStatus = JournalBatchStatus.Failed;
                result.Notes.Add($"Unexpected failure: {ex.Message}");
            }

            return result;
        }, cancellationToken);
    }

    private RenameOutcome ExecuteOne(RenamePlanEntry entry, JournalBatch batch)
    {
        try
        {
            var directory = Path.GetDirectoryName(entry.NewPath) ?? string.Empty;

            // A case-only rename (a.txt -> A.TXT) targets the same file on a
            // case-insensitive filesystem: the destination "exists" because it
            // IS the source, and Windows allows the rename.
            var isCaseOnlyRename =
                string.Equals(entry.OldPath, entry.NewPath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.OldPath, entry.NewPath, StringComparison.Ordinal);

            // Re-check the destination immediately before the move: the disk
            // may have changed since planning.
            if (!isCaseOnlyRename && (File.Exists(entry.NewPath) || Directory.Exists(entry.NewPath)))
            {
                // If the source vanished, report that distinctly.
                if (!File.Exists(entry.OldPath) && !Directory.Exists(entry.OldPath))
                    return Fail(entry, "Source file no longer exists.");

                return Fail(entry, "Destination already exists (appeared after preview).");
            }

            if (!File.Exists(entry.OldPath) && !Directory.Exists(entry.OldPath))
                return Fail(entry, "Source file no longer exists.");

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var isDirectory = entry.Item.Kind == ItemKind.Folder;

            if (isDirectory)
                Directory.Move(entry.OldPath, entry.NewPath);
            else
                File.Move(entry.OldPath, entry.NewPath, overwrite: false);

            // Journal immediately: this rename is now reversible.
            _journal.RecordSuccess(batch, entry.OldPath, entry.NewPath, new FileInfo(entry.NewPath));

            return new RenameOutcome
            {
                OriginalName = entry.Item.OriginalName,
                FinalName = Path.GetFileName(entry.NewPath),
                Succeeded = true
            };
        }
        catch (Exception ex)
        {
            return Fail(entry, ex.Message);
        }
    }

    private RenameOutcome Fail(RenamePlanEntry entry, string message) => new()
    {
        OriginalName = entry.Item.OriginalName,
        FinalName = Path.GetFileName(entry.NewPath),
        Succeeded = false,
        Error = message
    };
}

/// <summary>
/// Result of a whole rename batch.
/// </summary>
public class RenameBatchResult
{
    public List<RenameOutcome> Outcomes { get; } = new();
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public bool WasCancelled { get; set; }
    public string JournalBatchId { get; set; } = "";
    public JournalBatchStatus BatchStatus { get; set; }
    public List<string> Notes { get; } = new();
    public bool IsSuccess => Failed == 0 && !WasCancelled;
}
