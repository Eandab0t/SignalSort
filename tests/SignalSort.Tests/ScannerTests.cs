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
/// Scanner behavior on real temporary directories: recursion, filters,
/// progress, cancellation, reparse-point traversal and corrupt files.
/// </summary>
public class ScannerTests
{
    private static ScanProgress RunScan(string folderPath, bool recursive, bool includeHidden, bool includeSystem, out List<FileClassification> results)
    {
        var reports = new List<ScanProgress>();
        var scanner = CreateScanner();
        results = scanner.Scan(folderPath, recursive, includeHidden, includeSystem,
            new Progress<ScanProgress>(reports.Add), CancellationToken.None);
        return reports.LastOrDefault() ?? new ScanProgress();
    }

    private static FileScanner CreateScanner() =>
        new(new FileClassifier(new MetadataReader()));

    private sealed class TempDir : IDisposable
    {
        public string FolderPath { get; }
        public TempDir()
        {
            FolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(FolderPath);
        }
        public string this[string name] => Path.Combine(FolderPath, name.Replace('/', Path.DirectorySeparatorChar));
        public void WriteFile(string name, byte[]? content = null)
        {
            var p = this[name];
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllBytes(p, content ?? new byte[] { 1, 2, 3 });
        }
        public void Dispose()
        {
            try { Directory.Delete(FolderPath, true); } catch { }
        }
    }

    [Fact]
    public void Scan_TopLevelOnly_ExcludesSubfolderFiles()
    {
        using var root = new TempDir();
        root.WriteFile("top.txt");
        root.WriteFile("sub/nested.txt");

        RunScan(root.FolderPath, recursive: false, includeHidden: false, includeSystem: false, out var results);

        Assert.Contains(results, r => r.OriginalName == "top.txt");
        Assert.DoesNotContain(results, r => r.OriginalName == "nested.txt");
    }

    [Fact]
    public void Scan_Recursive_IncludesSubfolderFilesAndFolders()
    {
        using var root = new TempDir();
        root.WriteFile("top.txt");
        root.WriteFile("sub/nested.txt");

        RunScan(root.FolderPath, recursive: true, includeHidden: false, includeSystem: false, out var results);

        Assert.Contains(results, r => r.OriginalName == "nested.txt" && r.Kind == ItemKind.File);
        Assert.Contains(results, r => r.OriginalName == "sub" && r.Kind == ItemKind.Folder);
        Assert.Contains(results, r => r.OriginalName == Path.GetFileName(root.FolderPath));
    }

    [Fact]
    public void Scan_HiddenFiles_ExcludedByDefault_IncludedWhenRequested()
    {
        using var root = new TempDir();
        root.WriteFile("visible.txt");
        root.WriteFile("secret.txt");
        File.SetAttributes(root["secret.txt"], FileAttributes.Hidden);

        RunScan(root.FolderPath, recursive: false, includeHidden: false, includeSystem: false, out var results);
        Assert.DoesNotContain(results, r => r.OriginalName == "secret.txt");
        Assert.Contains(results, r => r.OriginalName == "visible.txt");

        RunScan(root.FolderPath, recursive: false, includeHidden: true, includeSystem: false, out var hiddenResults2);
        Assert.Contains("secret.txt", hiddenResults2.Select(r => r.OriginalName));
    }

    [Fact]
    public void Scan_CorruptMediaFile_FailsGracefully()
    {
        using var root = new TempDir();
        root.WriteFile("broken.mp4", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        RunScan(root.FolderPath, recursive: false, includeHidden: false, includeSystem: false, out var results);

        var item = Assert.Single(results, r => r.OriginalName == "broken.mp4");
        Assert.Equal("UnknownVideo", item.Purpose);
        Assert.Equal(ClassificationConfidence.Unknown, item.Confidence);
    }

    [Fact]
    public void Scan_ReportsProgress_AndIsDeterministic()
    {
        using var root = new TempDir();
        root.WriteFile("a.txt");
        root.WriteFile("b.txt");
        root.WriteFile("c.txt");

        var scanner = CreateScanner();
        var results = scanner.Scan(root.FolderPath, false, false, false, null, CancellationToken.None);
        var reports = new List<ScanProgress>();
        scanner.Scan(root.FolderPath, false, false, false, new Progress<ScanProgress>(reports.Add), CancellationToken.None);

        Assert.Equal(3, results.Count(r => r.Kind == ItemKind.File));
        Assert.True(reports.Count > 0);
        var last = reports.Last();
        Assert.Equal(last.Total, last.Processed);

        var second = scanner.Scan(root.FolderPath, false, false, false, null, CancellationToken.None);
        Assert.Equal(
            results.Select(r => (r.OriginalName, r.Purpose)).OrderBy(x => x.OriginalName),
            second.Select(r => (r.OriginalName, r.Purpose)).OrderBy(x => x.OriginalName));
    }

    [Fact]
    public void Scan_Cancellation_StopsScan()
    {
        using var root = new TempDir();
        for (int i = 0; i < 50; i++)
            root.WriteFile($"f{i:00}.txt");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            CreateScanner().Scan(root.FolderPath, false, false, false, null, cts.Token));
    }

    [Fact]
    public void Scan_ReparsePoints_DoNotLoopForever()
    {
        using var root = new TempDir();
        root.WriteFile("outside/real.txt", new byte[] { 1 });

        // A junction pointing into the scanned tree must terminate recursion.
        var linkPath = root["link"];
        var created = TryCreateJunction(linkPath, root["outside"]);
        if (!created)
            return; // Junction creation unavailable in this environment.

        var scanner = CreateScanner();
        var results = scanner.Scan(root.FolderPath, true, false, false, null, CancellationToken.None);

        // Junction recursion must terminate: each real file appears at most
        // twice (direct path + via junction path).
        var hits = results.Count(r => r.OriginalName == "real.txt");
        Assert.True(hits > 0 && hits <= 2);
    }

    private static bool TryCreateJunction(string linkPath, string targetPath)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5000);
            return Directory.Exists(linkPath);
        }
        catch
        {
            return false;
        }
    }
}
