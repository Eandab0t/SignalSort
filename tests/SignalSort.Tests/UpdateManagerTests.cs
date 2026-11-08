using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SignalSort.Core;
using SignalSort.Infrastructure.Updates;

namespace SignalSort.Tests;

public class UpdateManagerTests
{
    [Fact]
    public async Task CheckForUpdates_ValidManifest_ReturnsUpdates()
    {
        var source = new TestManifestSource(new UpdateManifest
        {
            SchemaVersion = "1",
            Updates = new List<UpdateInfo>
            {
                new UpdateInfo
                {
                    Id = "app",
                    Name = "SignalSort",
                    Type = UpdateType.Application,
                    Version = "1.0.0",
                    MinimumCompatibleVersion = "0.1.0",
                    DownloadUrl = "u",
                    Sha256 = "x"
                }
            }
        });
        var manager = new UpdateManager(source, new UnverifiedSigner(), "0.3.0");

        var result = await manager.CheckForUpdatesAsync();

        Assert.True(result.IsAvailable);
        Assert.Single(result.Updates);
        Assert.True(result.ManifestResult.IsValid);
    }

    [Fact]
    public async Task CheckForUpdates_EmptyManifest_ReturnsNoUpdates()
    {
        var source = new TestManifestSource(new UpdateManifest { SchemaVersion = "1", Updates = new List<UpdateInfo>() });
        var manager = new UpdateManager(source, new UnverifiedSigner(), "0.3.0");

        var result = await manager.CheckForUpdatesAsync();

        Assert.False(result.IsAvailable);
        Assert.Empty(result.Updates);
        Assert.True(result.ManifestResult.IsValid);
    }

    [Fact]
    public async Task CheckForUpdates_FetchFailure_ReturnsFailure()
    {
        var source = new FailingManifestSource();
        var manager = new UpdateManager(source, new UnverifiedSigner(), "0.3.0");

        var result = await manager.CheckForUpdatesAsync();

        Assert.False(result.IsAvailable);
        Assert.False(result.ManifestResult.IsValid);
    }

    [Fact]
    public async Task CheckForUpdates_IncompatibleManifest_IsUnavailableWithoutUpdates()
    {
        var source = new TestManifestSource(new UpdateManifest
        {
            SchemaVersion = "1",
            Updates = new List<UpdateInfo>
            {
                new UpdateInfo
                {
                    Id = "app",
                    Version = "9.0.0",
                    MinimumCompatibleVersion = "9.0.0",
                    DownloadUrl = "u",
                    Sha256 = "x"
                }
            }
        });
        var manager = new UpdateManager(source, new UnverifiedSigner(), "0.3.0");

        var result = await manager.CheckForUpdatesAsync();

        Assert.False(result.IsAvailable);
        Assert.False(result.ManifestResult.IsValid);
        Assert.Empty(result.Updates);
    }

    [Fact]
    public async Task DownloadUpdate_ReturnsDownloadedBytes()
    {
        var expected = Encoding.UTF8.GetBytes("package-bytes");

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();

            // Drain the request headers; they terminate at the blank line.
            var buffer = new byte[4096];
            var received = new StringBuilder();
            while (!received.ToString().Contains("\r\n\r\n"))
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0) break;
                received.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Length: {expected.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, 0, header.Length);
            await stream.WriteAsync(expected, 0, expected.Length);
        });

        try
        {
            var manager = new UpdateManager(
                new TestManifestSource(new UpdateManifest()), new UnverifiedSigner(), "0.3.0");
            var data = await manager.DownloadUpdateAsync(
                new UpdateInfo { DownloadUrl = $"http://127.0.0.1:{port}/pkg" });

            Assert.Equal(expected, data);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void VerifyPackage_CorrectHash_Passes()
    {
        var data = Encoding.UTF8.GetBytes("package");
        var hash = ComputeSha256(data);
        var manager = new UpdateManager(new TestManifestSource(new UpdateManifest()), new AllowAllVerifier(), "0.3.0");

        var result = manager.VerifyPackage(data, new UpdateInfo { Sha256 = hash, Signature = "ff" });

        Assert.True(result.IsValid, result.Reason);
    }

    [Fact]
    public void VerifyPackage_WrongHash_Fails()
    {
        var data = Encoding.UTF8.GetBytes("package");
        var manager = new UpdateManager(new TestManifestSource(new UpdateManifest()), new UnverifiedSigner(), "0.3.0");

        var result = manager.VerifyPackage(data, new UpdateInfo { Sha256 = "deadbeef" });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void VerifyPackage_UnverifiedSignature_Fails()
    {
        var data = Encoding.UTF8.GetBytes("package");
        var hash = ComputeSha256(data);
        var manager = new UpdateManager(new TestManifestSource(new UpdateManifest()), new UnverifiedSigner(), "0.3.0");

        var result = manager.VerifyPackage(data, new UpdateInfo { Sha256 = hash, Signature = "ff" });

        Assert.False(result.IsValid);
        Assert.Contains("signature", result.Reason);
    }

    [Fact]
    public void StagePackage_WritesPackageFile()
    {
        var manager = new UpdateManager(new TestManifestSource(new UpdateManifest()), new UnverifiedSigner(), "0.3.0");
        var data = Encoding.UTF8.GetBytes("package");

        var path = manager.StagePackage(data, new UpdateInfo { Id = "app", Version = "1.0.0" });

        Assert.True(File.Exists(path));
        Assert.Equal(data, File.ReadAllBytes(path));
        Assert.EndsWith(".zip", path);
    }

    [Fact]
    public async Task StageContentPack_ExtractsZip()
    {
        var manager = new UpdateManager(new TestManifestSource(new UpdateManifest()), new UnverifiedSigner(), "0.3.0");
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry("motif.txt");
            using var sw = new StreamWriter(entry.Open());
            await sw.WriteAsync("motif-data");
        }
        var data = ms.ToArray();

        var dir = manager.StageContentPack(data, new UpdateInfo { Id = "motifs", Version = "1.0.0" });

        Assert.True(File.Exists(Path.Combine(dir, "motif.txt")));
        Assert.Equal("motif-data", File.ReadAllText(Path.Combine(dir, "motif.txt")));
    }

    private static string ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private sealed class TestManifestSource : IUpdateSource
    {
        private readonly UpdateManifest _manifest;
        public TestManifestSource(UpdateManifest manifest) => _manifest = manifest;
        public Task<UpdateManifest?> FetchManifestAsync(string channel, CancellationToken cancellationToken = default) =>
            Task.FromResult<UpdateManifest?>(_manifest);
    }

    private sealed class FailingManifestSource : IUpdateSource
    {
        public Task<UpdateManifest?> FetchManifestAsync(string channel, CancellationToken cancellationToken = default) =>
            throw new IOException("network down");
    }

    private sealed class AllowAllVerifier : ISignatureVerifier
    {
        public bool Verify(byte[] data, byte[] signature, out string reason)
        {
            reason = string.Empty;
            return true;
        }
    }
}
