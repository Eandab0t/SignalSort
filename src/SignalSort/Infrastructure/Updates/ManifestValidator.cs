using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SignalSort.Infrastructure.Updates;

/// <summary>
/// Result of verifying a downloaded update package.
/// </summary>
public sealed class VerificationResult
{
    public bool IsValid { get; }
    public string Reason { get; } = string.Empty;

    private VerificationResult(bool isValid, string reason)
    {
        IsValid = isValid;
        Reason = reason;
    }

    public static VerificationResult Success() => new(true, string.Empty);
    public static VerificationResult Failure(string reason) => new(false, reason);
}

/// <summary>
/// Validates update manifests and downloaded packages.
///
/// Manifest validation checks schema version and compatibility with the
/// installed application. Package verification checks the SHA-256 hash and
/// the signature. Signature verification is delegated to ISignatureVerifier;
/// the shipped verifier rejects everything until a trusted key is configured.
/// </summary>
public sealed class ManifestValidator
{
    private const string SupportedSchema = "1";
    private readonly ISignatureVerifier _verifier;
    private readonly string _appVersion;

    public ManifestValidator(ISignatureVerifier verifier, string appVersion)
    {
        _verifier = verifier;
        _appVersion = appVersion;
    }

    /// <summary>
    /// Validates a parsed manifest: schema version and compatibility of every
    /// update with the installed application version.
    /// </summary>
    public ValidationResult ValidateManifest(UpdateManifest manifest)
    {
        if (manifest == null)
            return ValidationResult.Failure("manifest is empty");

        if (manifest.SchemaVersion != SupportedSchema)
            return ValidationResult.Failure($"unsupported manifest schema {manifest.SchemaVersion}");

        foreach (var update in manifest.Updates ?? Enumerable.Empty<UpdateInfo>())
        {
            if (!IsCompatible(update.MinimumCompatibleVersion, _appVersion))
                return ValidationResult.Failure(
                    $"update {update.Id} {update.Version} requires {update.MinimumCompatibleVersion}, " +
                    $"but this application is {_appVersion}");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Verifies a downloaded package: SHA-256 hash, then signature.
    /// </summary>
    public VerificationResult VerifyPackage(byte[] package, UpdateInfo update)
    {
        var hash = ComputeSha256(package);
        if (!string.Equals(hash, update.Sha256, StringComparison.OrdinalIgnoreCase))
            return VerificationResult.Failure("package hash does not match manifest");

        if (!_verifier.Verify(package, HexToBytes(update.Signature), out var reason))
            return VerificationResult.Failure($"signature verification failed: {reason}");

        return VerificationResult.Success();
    }

    private static bool IsCompatible(string minimum, string current)
    {
        var required = ParseVersion(minimum);
        var currentVersion = ParseVersion(current);
        for (int i = 0; i < 3; i++)
        {
            if (currentVersion[i] < required[i]) return false;
            if (currentVersion[i] > required[i]) return true;
        }
        return true;
    }

    private static int[] ParseVersion(string version)
    {
        var parts = (version ?? "0.0.0").Split('.');
        var numbers = new int[3];
        for (int i = 0; i < 3 && i < parts.Length; i++)
            int.TryParse(parts[i], out numbers[i]);
        return numbers;
    }

    private static string ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static byte[] HexToBytes(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}
