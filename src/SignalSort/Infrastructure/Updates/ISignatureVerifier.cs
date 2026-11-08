namespace SignalSort.Infrastructure.Updates;

/// <summary>
/// Verifies the authenticity of update packages.
///
/// The shipped verifier rejects everything: no signing key is provisioned,
/// so updates cannot be verified until an operator installs a trusted key.
/// Do not ship a default key here.
/// </summary>
public interface ISignatureVerifier
{
    /// <summary>
    /// Verifies a detached signature over downloaded package bytes.
    /// </summary>
    /// <returns>True when the signature verifies against a trusted key.</returns>
    bool Verify(byte[] data, byte[] signature, out string reason);
}

/// <summary>
/// Verifier that rejects every signature. Used until an operator installs
/// a trusted signing key.
/// </summary>
public sealed class UnverifiedSigner : ISignatureVerifier
{
    public bool Verify(byte[] data, byte[] signature, out string reason)
    {
        reason = "no trusted signing key configured";
        return false;
    }
}
