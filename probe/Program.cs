using System.IO;
using SignalSort.Core;
using SignalSort.Models;

var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Directory.CreateDirectory(Path.Combine(root, "Sub"));
File.WriteAllText(Path.Combine(root, "Sub", "a.txt"), "alpha");

var sub = new DirectoryInfo(Path.Combine(root, "Sub"));
var a = new FileInfo(Path.Combine(root, "Sub", "a.txt"));

var items = new List<FileClassification>
{
    new() { OriginalName = sub.Name, NewName = "Renamed", FullPath = sub.FullName, Kind = ItemKind.Folder, IsSelected = true },
    new() { OriginalName = a.Name, NewName = "b.txt", FullPath = a.FullName, Extension = ".txt", IsSelected = true },
};

var plan = new RenamePlanner().Plan(items);
foreach (var e in plan.Entries)
    Console.WriteLine($"[{e.Status}] {e.OldPath} -> {e.NewPath}  ({e.Note})");

var journal = new RenameJournal(Path.Combine(root, "j.json"));
var result = await new RenameExecutor(journal).ExecuteAsync(plan, root, null, CancellationToken.None);
Console.WriteLine($"Succeeded={result.Succeeded} Failed={result.Failed} Cancelled={result.WasCancelled}");
foreach (var o in result.Outcomes)
    Console.WriteLine($"  {(o.Succeeded ? "OK " : "ERR")} {o.OriginalName} -> {o.FinalName} {o.Error}");

Console.WriteLine("Renamed dir exists: " + Directory.Exists(Path.Combine(root, "Renamed")));
Console.WriteLine("b.txt exists: " + File.Exists(Path.Combine(root, "Renamed", "b.txt")));
try { Directory.Delete(root, true); } catch { }
