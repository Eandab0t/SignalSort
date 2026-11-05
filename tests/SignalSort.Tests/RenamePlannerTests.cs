using System.Collections.Generic;
using System.IO;
using Xunit;
using SignalSort.Core;
using SignalSort.Models;

namespace SignalSort.Tests;

public class RenamePlannerTests
{
    private static FileClassification Item(string name, string newName, string dir = @"C:\Music")
        => new()
        {
            OriginalName = name,
            NewName = newName,
            FullPath = Path.Combine(dir, name),
            Extension = Path.GetExtension(name),
        };

    [Fact]
    public void Plan_ReadyWhenNamesDiffer()
    {
        var plan = new RenamePlanner().Plan(new[] { Item("a.txt", "b.txt") });
        var e = Assert.Single(plan.Entries);
        Assert.Equal(RenamePlanStatus.Ready, e.Status);
        Assert.Equal(Path.Combine(@"C:\Music", "b.txt"), e.NewPath);
    }

    [Fact]
    public void Plan_NoOpWhenNameUnchanged()
    {
        var plan = new RenamePlanner().Plan(new[] { Item("a.txt", "a.txt") });
        Assert.Equal(RenamePlanStatus.NoOp, plan.Entries[0].Status);
        Assert.Equal(0, plan.ReadyCount);
    }

    [Fact]
    public void Plan_CaseInsensitiveCollisionWithinBatch()
    {
        // a.txt -> B.TXT and c.txt -> b.txt target the same destination
        // (case-insensitive), so both are flagged as collisions.
        var plan = new RenamePlanner().Plan(
            new[] { Item("a.txt", "B.TXT"), Item("c.txt", "b.txt") });
        Assert.Equal(2, plan.CollisionCount);
        Assert.All(plan.Entries, e => Assert.Equal(RenamePlanStatus.Collision, e.Status));
    }

    [Fact]
    public void Plan_DestinationOccupiedByUnrelatedFile_IsCollision()
    {
        using var root = new TempDir();
        File.WriteAllText(root["existing.txt"], "x");
        var plan = new RenamePlanner().Plan(new[] { Item("a.txt", "existing.txt", root.FolderPath) });
        Assert.Equal(RenamePlanStatus.Collision, plan.Entries[0].Status);
        Assert.Equal(1, plan.CollisionCount);
    }

    [Fact]
    public void Plan_SourceIsNotItsOwnDestination()
    {
        // a.txt -> b.txt where b.txt doesn't exist: ready
        var plan = new RenamePlanner().Plan(new[] { Item("a.txt", "b.txt") });
        Assert.Equal(RenamePlanStatus.Ready, plan.Entries[0].Status);
    }

    [Fact]
    public void Plan_Chain_OrderedSoSourceMovesFirst()
    {
        // a.txt -> b.txt ; b.txt -> c.txt
        var plan = new RenamePlanner().Plan(
            new[] { Item("a.txt", "b.txt"), Item("b.txt", "c.txt") });
        var statuses = new List<RenamePlanStatus>();
        foreach (var e in plan.Entries) statuses.Add(e.Status);
        Assert.Equal(new[] { RenamePlanStatus.Ready, RenamePlanStatus.Ready }, statuses);
        // b.txt->c.txt must come after a.txt->b.txt
        // b.txt->c.txt has no dependency, so it runs first; then a.txt->b.txt.
        var idxA = plan.Entries.FindIndex(e => e.Item.OriginalName == "a.txt");
        var idxB = plan.Entries.FindIndex(e => e.Item.OriginalName == "b.txt");
        Assert.True(idxB < idxA);
    }

    [Fact]
    public void Plan_Cycle_IsBlocked()
    {
        // a.txt -> b.txt ; b.txt -> a.txt
        var plan = new RenamePlanner().Plan(
            new[] { Item("a.txt", "b.txt"), Item("b.txt", "a.txt") });
        Assert.Equal(2, plan.CycleCount);
        Assert.All(plan.Entries, e => Assert.Equal(RenamePlanStatus.Cycle, e.Status));
    }

    [Fact]
    public void Plan_CaseInsensitiveChain()
    {
        // a.TXT -> b.txt ; b.txt -> c.txt (chain across case)
        var plan = new RenamePlanner().Plan(
            new[] { Item("a.TXT", "b.txt"), Item("b.txt", "c.txt") });
        Assert.Equal(2, plan.ReadyCount);
        Assert.Equal(0, plan.CycleCount);
    }

    [Fact]
    public void Plan_EmptyInput()
    {
        var plan = new RenamePlanner().Plan(new List<FileClassification>());
        Assert.Empty(plan.Entries);
    }

    private sealed class TempDir : IDisposable
    {
        public string FolderPath { get; }
        public TempDir()
        {
            FolderPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(FolderPath);
        }
        public string this[string name] => System.IO.Path.Combine(FolderPath, name);
        public void Dispose()
        {
            try { System.IO.Directory.Delete(FolderPath, true); } catch { }
        }
    }
}
