using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SignalSort.Models;

namespace SignalSort.Core;

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
            // Only a byte-identical name is a no-op. A case-only rename
            // (a.txt -> A.TXT) is a real change on Windows and is allowed.
            var sameName = string.Equals(
                Path.GetFileName(entry.OldPath),
                Path.GetFileName(entry.NewPath),
                StringComparison.Ordinal);

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
                continue;
            }

            // A folder cannot be renamed into its own subtree.
            if (entry.Item.Kind == ItemKind.Folder &&
                IsStrictlyInside(entry.NewPath, entry.OldPath))
            {
                entry.Status = RenamePlanStatus.Cycle;
                entry.Note = "A folder cannot be renamed into its own subfolder.";
            }
        }

        // Pass 3.5: destinations inside a folder that is itself being renamed.
        // If X moves into the folder's CURRENT location, the folder's own
        // rename would afterwards move X again, leaving the journal pointing
        // at a path that no longer matches reality. Block X; the folder
        // rename still runs and X can be retried afterwards.
        var folderRenames = plan.Entries
            .Where(e => e.Status == RenamePlanStatus.Ready &&
                        e.Item.Kind == ItemKind.Folder &&
                        !string.Equals(e.OldPath, e.NewPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var entry in plan.Entries.Where(e => e.Status == RenamePlanStatus.Ready))
        {
            var blocked = folderRenames.Any(f =>
                !ReferenceEquals(f, entry) &&
                !IsStrictlyInside(entry.OldPath, f.OldPath) &&
                IsStrictlyInside(entry.NewPath, f.OldPath));

            if (blocked)
            {
                entry.Status = RenamePlanStatus.Collision;
                entry.Note = "Destination is inside a folder that is also being renamed in this batch.";
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
    /// Re-orders Ready entries so that dependent renames execute in a safe
    /// order (Kahn's algorithm). Three dependency rules:
    ///   1. If e's destination is another entry's source, that source must
    ///      move away first (plain chains).
    ///   2. If e is a folder being renamed, every entry still living inside
    ///      e's source subtree must move first - renaming the folder would
    ///      otherwise invalidate their recorded paths.
    ///   3. If e's destination is inside a folder that is itself being
    ///      renamed, that folder must be renamed first so the destination
    ///      path is final when e executes.
    /// Entries left unordered form genuine cycles and are blocked.
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
            var free = remaining
                .Where(e => !remaining.Any(o => !ReferenceEquals(o, e) && Blocks(o, e)))
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

    /// <summary>
    /// True when o must execute before e can safely run.
    /// </summary>
    private static bool Blocks(RenamePlanEntry o, RenamePlanEntry e)
    {
        // 1. Chain: e's destination is o's source; o must vacate it first.
        if (string.Equals(o.OldPath, e.NewPath, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. e is a folder being renamed and o still lives inside e's source
        //    subtree: o must move (or be excluded) before e's rename, otherwise
        //    e's rename would invalidate o's recorded source path.
        if (e.Item.Kind == ItemKind.Folder &&
            !string.Equals(e.OldPath, e.NewPath, StringComparison.OrdinalIgnoreCase) &&
            IsStrictlyInside(o.OldPath, e.OldPath))
            return true;

        // 3. e's destination sits inside the folder o is renaming into; the
        //    folder must reach its final location before e can target it.
        if (o.Item.Kind == ItemKind.Folder &&
            !string.Equals(o.OldPath, o.NewPath, StringComparison.OrdinalIgnoreCase) &&
            IsStrictlyInside(e.NewPath, o.NewPath))
            return true;

        return false;
    }

    /// <summary>
    /// True when <paramref name="path"/> lies strictly inside the directory
    /// <paramref name="directory"/> (case-insensitive, Windows semantics).
    /// </summary>
    private static bool IsStrictlyInside(string path, string directory)
    {
        if (string.IsNullOrEmpty(directory))
            return false;

        var dir = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (dir.Length == 0)
            return false;

        return path.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(dir + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
