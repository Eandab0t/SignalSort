using System;
using System.Collections.Generic;

namespace SignalSort.Models;

/// <summary>
/// One recorded rename inside a journal batch. Enough information to reverse it:
/// where the file came from and where it went, plus a fingerprint of the moved
/// file so we can detect that it was modified afterwards.
/// </summary>
public class JournalEntry
{
    public string OriginalPath { get; set; } = "";
    public string NewPath { get; set; } = "";
    public bool Succeeded { get; set; }

    /// <summary>Length of the file at the time of the rename (bytes).</summary>
    public long FileLength { get; set; }

    /// <summary>Last write time (UTC ticks) of the file at the time of the rename.</summary>
    public long LastWriteUtcTicks { get; set; }

    /// <summary>Set during undo when an entry could not be reversed.</summary>
    public string? UndoNote { get; set; }
}

/// <summary>
/// A recorded rename batch. Written incrementally: entries are appended as they
/// succeed, and the batch is marked Completed (or Failed) when execution ends.
/// </summary>
public class JournalBatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public JournalBatchStatus Status { get; set; } = JournalBatchStatus.InProgress;
    public string Folder { get; set; } = "";
    public List<JournalEntry> Entries { get; set; } = new();
}

public enum JournalBatchStatus
{
    InProgress,
    Completed,
    Failed
}
