using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SignalSort.Core;
using SignalSort.Models;

namespace SignalSort.Tests;

/// <summary>
/// Regression tests for renaming files and folders that live inside each
/// other - the case where one rename can invalidate another entry's paths.
/// All tests use temporary directories only.
/// </summary>
public class NestedRenameSafetyTests
{
    [Fact]
    public async Task FileAndItsParentDirectory_BothRenamed_BothSucceed()
    {
        using var root = new TempDir();
        var sub = Directory.CreateDirectory(root["Sub"]);
        var a = root.File("Sub/a.txt", "alpha");

        var items = new List<FileClassification>
        {
            Item(sub.FullName, "Renamed", ItemKind.Folder),
            Item(a.FullName, "b.txt"),
        };
        var plan = new RenamePlanner().Plan(items);
        Assert.Equal(2, plan.ReadyCount);

        var journal = new RenameJournal(root["j.json"]);
        var result = await new RenameExecutor(journal).ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Succeeded);
        Assert.False(Directory.Exists(sub.FullName));
        Assert.True(Directory.Exists(root["Renamed"]));
        Assert.True(File.Exists(root["Renamed/b.txt"]));
        Assert.Equal("alpha", File.ReadAllText(root["Renamed/b.txt"]));
    }

    [Fact]
    public async Task NestedDirectories_DeepFirst()
    {
        using var root = new TempDir();
        Directory.CreateDirectory(root["Outer/Inner"]);
        root.File("Outer/Inner/deep.txt", "deep");

        var items = new List<FileClassification>
        {
            Item(root["Outer/Inner"], "InnerNew", ItemKind.Folder),
            Item(root["Outer"], "OuterNew", ItemKind.Folder),
        };
        var plan = new RenamePlanner().Plan(items);

        // Inner must be planned before Outer.
        var idxInner = plan.Entries.FindIndex(e => e.Item.OriginalName == "Inner");
        var idxOuter = plan.Entries.FindIndex(e => e.Item.OriginalName == "Outer");
        Assert.True(idxInner < idxOuter, "inner directory must be renamed before its parent");

        var journal = new RenameJournal(root["j.json"]);
        var result = await new RenameExecutor(journal).ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(Directory.Exists(root["OuterNew/InnerNew"]));
        Assert.True(File.Exists(root["OuterNew/InnerNew/deep.txt"]));
    }

    [Fact]
    public async Task Undo_AfterNestedRenames_RestoresEverything()
    {
        using var root = new TempDir();
        var sub = Directory.CreateDirectory(root["Sub"]);
        var a = root.File("Sub/a.txt", "alpha");

        var items = new List<FileClassification>
        {
            Item(sub.FullName, "Renamed", ItemKind.Folder),
            Item(a.FullName, "b.txt"),
        };
        var plan = new RenamePlanner().Plan(items);
        var journal = new RenameJournal(root["j.json"]);
        await new RenameExecutor(journal).ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        var undone = journal.UndoLast(out _);
        Assert.True(undone.IsSuccess);
        Assert.Equal(2, undone.Restored);

        // Directory and file are back at their original locations.
        Assert.True(Directory.Exists(sub.FullName));
        Assert.False(Directory.Exists(root["Renamed"]));
        Assert.True(File.Exists(a.FullName));
        Assert.Equal("alpha", File.ReadAllText(a.FullName));
    }

    [Fact]
    public void FolderIntoItsOwnSubtree_IsBlocked()
    {
        using var root = new TempDir();
        Directory.CreateDirectory(root["Sub"]);

        var items = new List<FileClassification>
        {
            Item(root["Sub"], "Sub/Nested", ItemKind.Folder),
        };
        var plan = new RenamePlanner().Plan(items);
        var entry = Assert.Single(plan.Entries);
        Assert.Equal(RenamePlanStatus.Cycle, entry.Status);
    }

    [Fact]
    public async Task DestinationInsideRenamedFolder_FromOutside_IsBlocked()
    {
        // Moving a file INTO a folder that is itself being renamed in the same
        // batch would leave the journal pointing at a path that moves again,
        // so the planner must block the file entry.
        using var root = new TempDir();
        Directory.CreateDirectory(root["Sub"]);
        var outside = root.File("outside.txt", "x");

        var items = new List<FileClassification>
        {
            Item(root["Sub"], "Renamed", ItemKind.Folder),
            Item(outside.FullName, "Sub/moved.txt"),
        };
        var plan = new RenamePlanner().Plan(items);

        var fileEntry = plan.Entries.Single(e => e.Item.OriginalName == "outside.txt");
        Assert.NotEqual(RenamePlanStatus.Ready, fileEntry.Status);

        // The folder rename itself still executes; the file stays put.
        var ready = plan.Entries.Where(e => e.Status == RenamePlanStatus.Ready).ToList();
        var journal = new RenameJournal(root["j.json"]);
        var result = await new RenameExecutor(journal).ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);
        Assert.Equal(1, result.Succeeded);
        Assert.True(Directory.Exists(root["Renamed"]));
        Assert.True(File.Exists(outside.FullName));
    }

    [Fact]
    public async Task PartialFailure_WithNestedEntries_JournalStaysReversible()
    {
        using var root = new TempDir();
        var sub = Directory.CreateDirectory(root["Sub"]);
        var a = root.File("Sub/a.txt", "alpha");
        var x = root.File("x.txt", "x");
        // blocker.txt is a file - renaming x.txt into blocker.txt/inner.txt
        // fails at runtime while the nested renames succeed.
        root.File("blocker.txt", "blocker");

        var items = new List<FileClassification>
        {
            Item(sub.FullName, "Renamed", ItemKind.Folder),
            Item(a.FullName, "b.txt"),
            Item(x.FullName, "blocker.txt/inner.txt"),
        };
        var plan = new RenamePlanner().Plan(items);
        var journal = new RenameJournal(root["j.json"]);
        var result = await new RenameExecutor(journal).ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.True(Directory.Exists(root["Renamed"]));
        Assert.True(File.Exists(root["Renamed/b.txt"]));

        // Undo restores the nested renames; the failed entry was never journaled.
        var undone = journal.UndoLast(out _);
        Assert.True(undone.IsSuccess);
        Assert.Equal(2, undone.Restored);
        Assert.True(Directory.Exists(sub.FullName));
        Assert.True(File.Exists(a.FullName));
        Assert.Equal("alpha", File.ReadAllText(a.FullName));
        Assert.True(File.Exists(x.FullName));
    }

    private static FileClassification Item(string fullPath, string newName, ItemKind kind = ItemKind.File)
        => new()
        {
            OriginalName = System.IO.Path.GetFileName(fullPath),
            NewName = newName,
            FullPath = fullPath,
            Extension = kind == ItemKind.File ? System.IO.Path.GetExtension(fullPath) : "",
            Kind = kind,
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
        public string this[string name] => System.IO.Path.Combine(FolderPath, name.Replace('/', System.IO.Path.DirectorySeparatorChar));
        public FileInfo File(string name, string content)
        {
            var p = this[name];
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p)!);
            System.IO.File.WriteAllBytes(p, System.Text.Encoding.UTF8.GetBytes(content));
            return new FileInfo(p);
        }
        public void Dispose()
        {
            try { System.IO.Directory.Delete(FolderPath, true); } catch { }
        }
    }
}
