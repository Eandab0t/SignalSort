using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartFileClassifier.Models;

namespace SmartFileClassifier.Core;

/// <summary>
/// Builds an executable rename plan from the selected classifications.
///
/// Planning never touches the disk (except metadata-free existence checks).
/// It detects every hazard up front:
///   - destinations that already exist on disk,
///   - two items in the batch claiming the same destination (case-insensitive,
///     because Windows file systems are case-insensitive),
///   - chained renames where one item's destination is another item's source
///     (the plan is ordered so chains execute safely),
///   - genuine cycles (a.txt to b.txt and b.txt to a.txt), which are refused.
///
/// Within-batch duplicate destinations that are not on disk are de-duplicated
/// deterministically and surfaced as adjustments, never applied silently:
/// the caller shows the final planned names before execution.
/// </summary>
public class RenamePlanner
{
    public RenamePlan Plan(IEnumerable<FileClassification> selected)
    {
        var plan = new RenamePlan();
        var items = selected.ToList();

        // Pass 1: build raw entries for every selected item.
        var raw = new List<(FileClassification Item, string OldPath, string NewPath)>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.FullPath))
                continue;

            var oldPath = item.FullPath;
            var newName = string.IsNullOrWhiteSpace(item.NewName)
                ? Path.GetFileName(oldPath)
                : item.NewName;
            var newPath = Path.Combine(Path.GetDirectoryName(oldPath) ?? string.Empty, newName);

            raw.Add((item, oldPath, newPath));
        }

        // Pass 2: assign each entry its requested destination, then flag any
        // destination claimed by more than one item as a within-batch collision.
        // We never silently renumber - the user sees every conflict.
        var claims = new List<(string NewPath, string OldPath)>();
        foreach (var entry in raw)
            claims.Add((entry.NewPath, entry.OldPath));

        var duplicateDestinations = claims
            .GroupBy(c => c.NewPath, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in raw)
        {
            var isDuplicate = duplicateDestinations.Contains(entry.NewPath);

            plan.Entries.Add(new RenamePlanEntry
            {
                Item = entry.Item,
                OldPath = entry.OldPath,
                NewPath = entry.NewPath,
                Status = isDuplicate ? RenamePlanStatus.Collision : RenamePlanStatus.Ready,
                Note = isDuplicate
                    ? "Duplicate destination within this batch."
                    : ""
            });
        }

        // Reflect planned names back so the UI shows the truth.
        foreach (var entry in plan.Entries)
            entry.Item.NewName = Path.GetFileName(entry.NewPath);

        // Pass 3: classify each entry.
        foreach (var entry in plan.Entries)
        {
            var sameName = string.Equals(
                Path.GetFileName(entry.OldPath),
                Path.GetFileName(entry.NewPath),
                StringComparison.OrdinalIgnoreCase);

            if (sameName)
            {
                entry.Status = RenamePlanStatus.NoOp;
                entry.Note = "Name is unchanged.";
                continue;
            }

            if (DestinationOccupied(entry.NewPath, entry.OldPath, plan.Entries))
            {
                entry.Status = RenamePlanStatus.Collision;
                entry.Note = "A file or folder with this name already exists.";
            }
        }

        // Pass 4: order chained renames safely; detect cycles.
        OrderAndDetectCycles(plan);

        return plan;
    }

    /// <summary>
    /// True when newPath already exists on disk and is not the source itself
    /// (Windows paths are case-insensitive).
    /// </summary>
    private static bool DestinationOccupied(string newPath, string oldPath, List<RenamePlanEntry> entries)
    {
        // Another item moving away from that name does not count as occupied.
        foreach (var other in entries)
        {
            if (string.Equals(other.OldPath, newPath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(other.OldPath, other.NewPath, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (string.Equals(newPath, oldPath, StringComparison.OrdinalIgnoreCase))
            return false;

        return File.Exists(newPath) || Directory.Exists(newPath);
    }

    /// <summary>
    /// Re-orders Ready entries so that when one item's destination is another
    /// item's source, the item moving *away* executes first (Kahn's algorithm).
    /// Members of a genuine cycle are blocked with RenamePlanStatus.Cycle.
    /// </summary>
    private static void OrderAndDetectCycles(RenamePlan plan)
    {
        var movable = plan.Entries
            .Where(e => e.Status == RenamePlanStatus.Ready)
            .ToList();

        var ordered = new List<RenamePlanEntry>(movable.Count);
        var remaining = new List<RenamePlanEntry>(movable);

        while (remaining.Count > 0)
        {
            // Free = its destination is not the source of any remaining entry.
            var free = remaining
                .Where(e => !remaining.Any(o =>
                    !ReferenceEquals(o, e) &&
                    string.Equals(o.OldPath, e.NewPath, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (free.Count == 0)
            {
                // Everything left forms one or more swap cycles.
                foreach (var e in remaining)
                {
                    e.Status = RenamePlanStatus.Cycle;
                    e.Note = "This rename participates in a swap cycle and cannot be performed automatically.";
                }
                break;
            }

            ordered.AddRange(free);
            remaining.RemoveAll(free.Contains);
        }

        // Rewrite the plan's entry list in safe execution order:
        // Ready entries in dependency order, then everything else.
        var others = plan.Entries.Where(e => e.Status != RenamePlanStatus.Ready).ToList();
        plan.Entries.Clear();
        plan.Entries.AddRange(ordered);
        plan.Entries.AddRange(others);
    }
}
