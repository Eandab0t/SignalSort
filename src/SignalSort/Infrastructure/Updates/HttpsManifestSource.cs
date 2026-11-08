using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SignalSort.Infrastructure.Updates;

/// <summary>
/// Fetches update manifests over HTTPS.
///
/// Plain-HTTP manifest URLs are rejected at construction so a mistyped or
/// attacker-controlled setting can never downgrade the update channel to an
/// unencrypted transport. The source only reads the manifest; it makes no
/// trust decisions itself - packages listed by the manifest are verified
/// later by <see cref="ManifestValidator"/> (SHA-256 + signature).
/// </summary>
public sealed class HttpsManifestSource : IUpdateSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Uri _manifestUrl;
    private readonly HttpClient _client;

    /// <param name="manifestUrl">HTTPS URL of the update manifest.</param>
    /// <param name="client">Optional HttpClient; a default is created when omitted.</param>
    public HttpsManifestSource(Uri manifestUrl, HttpClient? client = null)
    {
        if (manifestUrl.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Manifest URL must use HTTPS.", nameof(manifestUrl));

        _manifestUrl = manifestUrl;
        _client = client ?? new HttpClient();
    }

    /// <summary>
    /// Downloads and parses the manifest for the requested channel. For any
    /// channel other than "stable" the channel is appended as a query
    /// parameter. A 404 response means "no manifest published yet" and
    /// yields null; any other non-success status throws.
    /// </summary>
    public async Task<UpdateManifest?> FetchManifestAsync(string channel, CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(UrlFor(channel), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, JsonOptions, cancellationToken);
    }

    private Uri UrlFor(string channel)
    {
        if (string.Equals(channel, "stable", StringComparison.OrdinalIgnoreCase))
            return _manifestUrl;

        var separator = string.IsNullOrEmpty(_manifestUrl.Query) ? "?" : "&";
        return new Uri($"{_manifestUrl}{separator}channel={Uri.EscapeDataString(channel)}");
    }
}
