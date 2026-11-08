namespace SignalSort.Infrastructure.Updates;

/// <summary>
/// Provides update manifests from a configured source.
///
/// Implementations fetch over HTTPS. The client must verify the manifest
/// signature before trusting any update it lists.
/// </summary>
public interface IUpdateSource
{
    /// <summary>
    /// Downloads the manifest for the given channel.
    /// </summary>
    /// <param name="channel">Release channel, e.g. "stable" or "beta".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The manifest, or null if the source has no updates.</param>
    Task<UpdateManifest?> FetchManifestAsync(string channel, CancellationToken cancellationToken = default);
}
