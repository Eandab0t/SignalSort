using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SmartFileClassifier.Core;
using SmartFileClassifier.Models;

namespace FileQuickNamie.Tests;

public class RenameExecutorAndJournalTests
{
    [Fact]
    public async Task Execute_RenamesSelectedItems()
    {
        using var root = new TempDir();
        var a = root.File("a.txt", "alpha");
        var b = root.File("b.txt", "beta");

        var items = new List<FileClassification>
        {
            Item(a.FullName, "apple.txt"),
            Item(b.FullName, "banana.txt"),
        };
        var plan = new RenamePlanner().Plan(items.Where(i => i.IsSelected));
        var journal = new RenameJournal(System.IO.Path.Combine(root.FolderPath, "journal.json"));
        var executor = new RenameExecutor(journal);

        var result = await executor.ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.False(File.Exists(a.FullName));
        Assert.False(File.Exists(b.FullName));
        Assert.True(File.Exists(root["apple.txt"]));
        Assert.True(File.Exists(root["banana.txt"]));
        Assert.Equal("alpha", File.ReadAllText(root["apple.txt"]));
        Assert.Equal("beta", File.ReadAllText(root["banana.txt"]));
    }

    [Fact]
    public async Task Undo_RestoresOriginalNames()
    {
        using var root = new TempDir();
        var a = root.File("a.txt", "alpha");
        var b = root.File("b.txt", "beta");

        var items = new List<FileClassification>
        {
            Item(a.FullName, "apple.txt"),
            Item(b.FullName, "banana.txt"),
        };
        var plan = new RenamePlanner().Plan(items);
        var journal = new RenameJournal(System.IO.Path.Combine(root.FolderPath, "journal.json"));
        var executor = new RenameExecutor(journal);
        var result = await executor.ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var undone = journal.UndoLast(out var batch);
        Assert.NotNull(batch);
        Assert.True(undone.IsSuccess);
        Assert.Equal(2, undone.Restored);
        Assert.Equal(0, undone.Skipped);
        Assert.True(File.Exists(a.FullName));
        Assert.True(File.Exists(b.FullName));
        Assert.False(File.Exists(root["apple.txt"]));
        Assert.Equal("alpha", File.ReadAllText(a.FullName));
    }

    [Fact]
    public async Task Undo_SkipsModifiedFile()
    {
        using var root = new TempDir();
        var a = root.File("a.txt", "alpha");

        var items = new List<FileClassification> { Item(a.FullName, "apple.txt") };
        var plan = new RenamePlanner().Plan(items);
        var journal = new RenameJournal(System.IO.Path.Combine(root.FolderPath, "journal.json"));
        var executor = new RenameExecutor(journal);
        await executor.ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        File.WriteAllText(root["apple.txt"], "tampered");

        var undone = journal.UndoLast(out _);
        Assert.False(undone.IsSuccess);
        Assert.Equal(1, undone.Skipped);
        Assert.True(File.Exists(root["apple.txt"]));
        Assert.Equal("tampered", File.ReadAllText(root["apple.txt"]));
        Assert.False(File.Exists(a.FullName));
    }

    [Fact]
    public async Task Planner_BlocksOccupiedDestination()
    {
        using var root = new TempDir();
        var a = root.File("a.txt", "alpha");
        File.WriteAllText(root["b.txt"], "preexisting");

        var items = new List<FileClassification> { Item(a.FullName, "b.txt") };
        var plan = new RenamePlanner().Plan(items);
        Assert.Equal(0, plan.ReadyCount);
        Assert.Equal(1, plan.CollisionCount);

        var journal = new RenameJournal(System.IO.Path.Combine(root.FolderPath, "journal.json"));
        var executor = new RenameExecutor(journal);
        var result = await executor.ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);
        Assert.Equal(0, result.Succeeded);
        Assert.True(File.Exists(a.FullName));
    }

    [Fact]
    public async Task PartialFailure_ContinuesAndReports()
    {
        using var root = new TempDir();
        var a = root.File("a.txt", "alpha");
        var c = root.File("c.txt", "gamma");
        // blocker.txt is a file - renaming c.txt into blocker.txt/inner.txt
        // cannot create the subdirectory, so that execution fails while
        // a.txt still succeeds.
        var blocker = root.File("blocker.txt", "blocker");

        var items = new List<FileClassification>
        {
            Item(a.FullName, "b.txt"), // succeeds
            Item(c.FullName, System.IO.Path.Combine("blocker.txt", "inner.txt")), // fails
        };
        var plan = new RenamePlanner().Plan(items);
        var journal = new RenameJournal(System.IO.Path.Combine(root.FolderPath, "journal.json"));
        var executor = new RenameExecutor(journal);
        var result = await executor.ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.False(result.IsSuccess);
        Assert.True(File.Exists(root["b.txt"]));
        Assert.False(File.Exists(a.FullName));
        Assert.True(File.Exists(c.FullName)); // not moved
        var failed = result.Outcomes.Single(o => !o.Succeeded);
        Assert.NotNull(failed.Error);
    }

    [Fact]
    public async Task Cancellation_StopsAndJournalsCompleted()
    {
        using var root = new TempDir();
        var files = new List<string>();
        for (int i = 0; i < 10; i++)
            files.Add(root.File($"f{i}.txt", $"content{i}").FullName);

        var items = files.Select((f, i) => Item(f, $"renamed{i}.txt")).ToList();
        var plan = new RenamePlanner().Plan(items);
        var journal = new RenameJournal(System.IO.Path.Combine(root.FolderPath, "journal.json"));
        var executor = new RenameExecutor(journal);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(50);
        var result = await executor.ExecuteAsync(plan, root.FolderPath, null, cts.Token);

        Assert.True(result.WasCancelled);
        Assert.True(result.Succeeded >= 0);
        // Any completed renames are journaled and undoable.
        if (result.Succeeded > 0)
            Assert.True(journal.CanUndo());
    }

    [Fact]
    public void Journal_PersistsAcrossInstances()
    {
        using var root = new TempDir();
        var journalPath = root["j.json"];
        var j1 = new RenameJournal(journalPath);
        var batch = j1.StartBatch(root.FolderPath);
        j1.RecordSuccess(batch, "/a.txt", "/apple.txt", new FileInfo(root.File("a.txt", "aa").FullName));
        j1.CompleteBatch(batch, JournalBatchStatus.Completed);

        var j2 = new RenameJournal(journalPath);
        Assert.True(j2.CanUndo());
        var recent = j2.RecentBatches(5);
        Assert.Single(recent);
        Assert.Equal(JournalBatchStatus.Completed, recent[0].Status);
    }

    [Fact]
    public void Journal_UndoLast_WhenNone()
    {
        using var root = new TempDir();
        var journal = new RenameJournal(root["j.json"]);
        var undone = journal.UndoLast(out var batch);
        Assert.Null(batch);
        Assert.False(undone.IsSuccess);
        Assert.Single(undone.Notes);
    }

    private static FileClassification Item(string fullPath, string newName)
        => new()
        {
            OriginalName = System.IO.Path.GetFileName(fullPath),
            NewName = newName,
            FullPath = fullPath,
            Extension = System.IO.Path.GetExtension(fullPath),
            IsSelected = true,
        };

    private sealed class TempDir : IDisposable
    {
        public string FolderPath { get; }
        public TempDir()
        {
            FolderPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(FolderPath);
        }
        public string this[string name] => System.IO.Path.Combine(FolderPath, name);
        public FileInfo File(string name, string content)
        {
            var p = this[name];
            System.IO.File.WriteAllBytes(p, System.Text.Encoding.UTF8.GetBytes(content));
            return new FileInfo(p);
        }
        public void Dispose()
        {
            try { System.IO.Directory.Delete(FolderPath, true); } catch { }
        }
    }
}
