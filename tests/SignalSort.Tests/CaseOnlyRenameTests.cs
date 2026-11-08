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
/// Case-only renames (a.txt -> A.TXT) on real Windows filesystems:
/// planning, execution, batch behavior and undo.
/// </summary>
public class CaseOnlyRenameTests
{
    [Fact]
    public void Planner_CaseOnlyRename_IsReady()
    {
        using var root = new TempDir();
        root.File("a.txt", "x");

        var plan = new RenamePlanner().Plan(new[] { Item(root["a.txt"], "A.TXT") });
        var entry = Assert.Single(plan.Entries);
        Assert.Equal(RenamePlanStatus.Ready, entry.Status);
    }

    [Fact]
    public async Task Execute_CaseOnlyFileRename_RenamesOnDisk()
    {
        using var root = new TempDir();
        var a = root.File("a.txt", "alpha");

        var plan = new RenamePlanner().Plan(new[] { Item(a.FullName, "A.TXT") });
        var journal = new RenameJournal(root["j.json"]);
        var result = await new RenameExecutor(journal).ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(root["A.TXT"]));
        Assert.Equal("alpha", File.ReadAllText(root["A.TXT"]));
        // Exact on-disk casing changed (File.Exists is case-insensitive).
        Assert.Equal("A.TXT", new DirectoryInfo(root.FolderPath).GetFiles("*.txt")[0].Name);
    }

    [Fact]
    public async Task Execute_CaseOnlyDirectoryRename_RenamesOnDisk()
    {
        using var root = new TempDir();
        var sub = Directory.CreateDirectory(root["Sub"]);
        root.File("Sub/f.txt", "content");

        var plan = new RenamePlanner().Plan(new[] { Item(sub.FullName, "SUB", ItemKind.Folder) });
        var journal = new RenameJournal(root["j.json"]);
        var result = await new RenameExecutor(journal).ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(Directory.Exists(root["SUB"]));
        Assert.True(File.Exists(root["SUB/f.txt"]));
        // Exact on-disk casing changed.
        Assert.Equal("SUB", new DirectoryInfo(root.FolderPath).GetDirectories()[0].Name);
    }

    [Fact]
    public async Task Batch_CaseOnlyRename_PlusOtherRename_BothSucceed()
    {
        using var root = new TempDir();
        var a = root.File("a.txt", "alpha");
        var b = root.File("b.txt", "beta");

        var items = new List<FileClassification>
        {
            Item(a.FullName, "A.TXT"),
            Item(b.FullName, "renamed.txt"),
        };
        var plan = new RenamePlanner().Plan(items);
        Assert.Equal(2, plan.ReadyCount);

        var journal = new RenameJournal(root["j.json"]);
        var result = await new RenameExecutor(journal).ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);

        Assert.Equal(2, result.Succeeded);
        Assert.True(File.Exists(root["A.TXT"]));
        Assert.True(File.Exists(root["renamed.txt"]));
    }

    [Fact]
    public async Task Undo_CaseOnlyRename_RestoresOriginalCasing()
    {
        using var root = new TempDir();
        var a = root.File("a.txt", "alpha");

        var plan = new RenamePlanner().Plan(new[] { Item(a.FullName, "A.TXT") });
        var journal = new RenameJournal(root["j.json"]);
        await new RenameExecutor(journal).ExecuteAsync(plan, root.FolderPath, null, CancellationToken.None);
        Assert.True(File.Exists(root["A.TXT"]));

        var undone = journal.UndoLast(out _);
        Assert.True(undone.IsSuccess);
        Assert.True(File.Exists(root["a.txt"]));
        Assert.Equal("alpha", File.ReadAllText(root["a.txt"]));
        // Exact original casing restored, not the renamed casing.
        Assert.Equal("a.txt", new DirectoryInfo(root.FolderPath).GetFiles("*.txt")[0].Name);
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
