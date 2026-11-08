using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SignalSort.Infrastructure.Updates;

/// <summary>
/// Outcome of checking an update source for new updates.
/// </summary>
public sealed class UpdateCheckResult
{
    public bool IsAvailable { get; }
    public ValidationResult ManifestResult { get; }
    public IReadOnlyList<UpdateInfo> Updates { get; }

    private UpdateCheckResult(bool isAvailable, ValidationResult manifestResult, IReadOnlyList<UpdateInfo> updates)
    {
        IsAvailable = isAvailable;
        ManifestResult = manifestResult;
        Updates = updates;
    }

    public static UpdateCheckResult Success(ValidationResult manifestResult, IReadOnlyList<UpdateInfo> updates)
        => new(manifestResult.IsValid && updates.Count > 0, manifestResult, updates);

    public static UpdateCheckResult Failure(string reason)
        => new(false, ValidationResult.Failure(reason), Array.Empty<UpdateInfo>());
}

/// <summary>
/// Coordinates update discovery, download, verification and staging.
///
/// The manager never applies changes silently: it downloads, verifies and
/// stages packages. Applying an update (especially the application itself)
/// requires user confirmation and, for application updates, a restart.
/// </summary>
public sealed class UpdateManager
{
    private readonly IUpdateSource _source;
    private readonly ManifestValidator _validator;
    private readonly string _stagingRoot;

    public UpdateManager(IUpdateSource source, ISignatureVerifier verifier, string appVersion, string? stagingRoot = null)
    {
        _source = source;
        _validator = new ManifestValidator(verifier, appVersion);
        _stagingRoot = stagingRoot ?? Path.Combine(Path.GetTempPath(), "SignalSort", "updates");
    }

    /// <summary>
    /// Checks the remote source for new updates and validates the manifest
    /// against this installation. Returns the list of applicable updates.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        string channel = "stable",
        CancellationToken cancellationToken = default)
    {
        UpdateManifest? manifest;
        try
        {
            manifest = await _source.FetchManifestAsync(channel, cancellationToken);
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failure($"failed to fetch manifest: {ex.Message}");
        }

        if (manifest == null)
            return UpdateCheckResult.Success(ValidationResult.Success(), Array.Empty<UpdateInfo>());

        var validation = _validator.ValidateManifest(manifest);
        return UpdateCheckResult.Success(
            validation,
            validation.IsValid ? manifest.Updates ?? new List<UpdateInfo>() : new List<UpdateInfo>());
    }

    /// <summary>
    /// Downloads the update package from its manifest URL.
    /// </summary>
    public async Task<byte[]> DownloadUpdateAsync(UpdateInfo update, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        return await client.GetByteArrayAsync(new Uri(update.DownloadUrl), cancellationToken);
    }

    /// <summary>
    /// Verifies a downloaded package: SHA-256 hash, then signature.
    /// </summary>
    public VerificationResult VerifyPackage(byte[] package, UpdateInfo update)
        => _validator.VerifyPackage(package, update);

    /// <summary>
    /// Stages a downloaded package in the staging area without applying it.
    /// Returns the path to the staged package file.
    /// </summary>
    public string StagePackage(byte[] package, UpdateInfo update)
    {
        var stageDir = Path.Combine(_stagingRoot, $"{update.Id}-{update.Version}");
        Directory.CreateDirectory(stageDir);
        var pkgPath = Path.Combine(stageDir, "package.zip");
        File.WriteAllBytes(pkgPath, package);
        return pkgPath;
    }

    /// <summary>
    /// Stages a content pack by extracting its package into the staging area.
    /// Returns the path to the extracted content directory.
    /// </summary>
    public string StageContentPack(byte[] package, UpdateInfo update)
    {
        var stageDir = Path.Combine(_stagingRoot, "content", update.Id, update.Version);
        Directory.CreateDirectory(stageDir);
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        zip.ExtractToDirectory(stageDir, overwriteFiles: true);
        return stageDir;
    }
}
