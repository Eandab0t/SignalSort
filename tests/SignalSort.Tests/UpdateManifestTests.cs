using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using SignalSort.Infrastructure.Updates;

namespace SignalSort.Tests;

public class ManifestValidatorTests
{
    private static readonly ManifestValidator Validator = new(new UnverifiedSigner(), "0.3.0");

    [Fact]
    public void ValidManifest_Passes()
    {
        var manifest = new UpdateManifest
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
                    MinimumCompatibleVersion = "0.3.0",
                    DownloadUrl = "https://example.com/SignalSort.zip",
                    Sha256 = "aaaa"
                }
            }
        };

        var result = Validator.ValidateManifest(manifest);

        Assert.True(result.IsValid, result.Reason);
    }

    [Fact]
    public void BadSchemaVersion_Fails()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = "2",
            Updates = new List<UpdateInfo>
            {
                new UpdateInfo
                {
                    Version = "1.0.0",
                    MinimumCompatibleVersion = "0.1.0",
                    DownloadUrl = "u",
                    Sha256 = "x"
                }
            }
        };

        var result = Validator.ValidateManifest(manifest);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void IncompatibleMinimumVersion_Fails()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = "1",
            Updates = new List<UpdateInfo>
            {
                new UpdateInfo
                {
                    Version = "2.0.0",
                    MinimumCompatibleVersion = "2.0.0",
                    DownloadUrl = "u",
                    Sha256 = "x"
                }
            }
        };

        var result = Validator.ValidateManifest(manifest);

        Assert.False(result.IsValid);
        Assert.Contains("0.3.0", result.Reason);
    }

    [Fact]
    public void CompatibleMinimumVersion_Passes()
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = "1",
            Updates = new List<UpdateInfo>
            {
                new UpdateInfo
                {
                    Version = "0.4.0",
                    MinimumCompatibleVersion = "0.3.0",
                    DownloadUrl = "u",
                    Sha256 = "x"
                }
            }
        };

        var result = Validator.ValidateManifest(manifest);

        Assert.True(result.IsValid, result.Reason);
    }

    [Fact]
    public void EmptyManifest_Passes()
    {
        var manifest = new UpdateManifest { SchemaVersion = "1", Updates = new List<UpdateInfo>() };

        var result = Validator.ValidateManifest(manifest);

        Assert.True(result.IsValid, result.Reason);
    }

    [Fact]
    public void NullManifest_Fails()
    {
        var result = Validator.ValidateManifest(null!);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void WrongHash_Fails()
    {
        var data = Encoding.UTF8.GetBytes("hello");
        var update = new UpdateInfo { Sha256 = "deadbeef", Signature = "ff" };

        var result = Validator.VerifyPackage(data, update);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CorrectHash_ButMissingSignature_Fails()
    {
        var data = Encoding.UTF8.GetBytes("hello");
        var hash = ComputeSha256(data);
        var update = new UpdateInfo { Sha256 = hash, Signature = string.Empty };

        var result = Validator.VerifyPackage(data, update);

        Assert.False(result.IsValid);
        Assert.Contains("signature", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorrectHashAndValidSignature_Passes()
    {
        var data = Encoding.UTF8.GetBytes("package");
        var hash = ComputeSha256(data);
        using var rsa = RSA.Create();
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var verifier = new RsaPublicKeyVerifier(rsa.ExportParameters(false));
        var validator = new ManifestValidator(verifier, "0.3.0");
        var update = new UpdateInfo { Sha256 = hash, Signature = Convert.ToHexString(signature) };

        var result = validator.VerifyPackage(data, update);

        Assert.True(result.IsValid, result.Reason);
    }

    private static string ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private sealed class RsaPublicKeyVerifier : ISignatureVerifier
    {
        private readonly RSAParameters _publicKey;

        public RsaPublicKeyVerifier(RSAParameters publicKey) => _publicKey = publicKey;

        public bool Verify(byte[] data, byte[] signature, out string reason)
        {
            using var rsa = RSA.Create();
            rsa.ImportParameters(_publicKey);
            var valid = rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            reason = valid ? string.Empty : "signature does not match trusted key";
            return valid;
        }
    }
}
