using System;
using System.Collections.Generic;

namespace SmartFileClassifier.Models;

/// <summary>
/// A single proposed rename within a batch, after pre-flight planning.
/// </summary>
public class RenamePlanEntry
{
    public FileClassification Item { get; init; } = null!;

    /// <summary>Full path before the rename.</summary>
    public string OldPath { get; init; } = "";

    /// <summary>Full path after the rename (final, de-conflicted).</summary>
    public string NewPath { get; init; } = "";

    public RenamePlanStatus Status { get; set; }

    /// <summary>Human-readable reason when the entry is not ready (collision etc.).</summary>
    public string Note { get; set; } = "";
}

public enum RenamePlanStatus
{
    /// <summary>Safe to execute.</summary>
    Ready,

    /// <summary>New name equals current name; nothing to do.</summary>
    NoOp,

    /// <summary>Blocked because the destination already exists or clashes within the batch.</summary>
    Collision,

    /// <summary>Part of a rename cycle that cannot be safely ordered.</summary>
    Cycle
}

/// <summary>
/// Result of planning a whole rename batch. Planning never touches the disk.
/// </summary>
public class RenamePlan
{
    public List<RenamePlanEntry> Entries { get; init; } = new();

    public int ReadyCount => Count(RenamePlanStatus.Ready);
    public int CollisionCount => Count(RenamePlanStatus.Collision);
    public int CycleCount => Count(RenamePlanStatus.Cycle);
    public int NoOpCount => Count(RenamePlanStatus.NoOp);

    private int Count(RenamePlanStatus status)
    {
        int n = 0;
        foreach (var e in Entries) if (e.Status == status) n++;
        return n;
    }
}
