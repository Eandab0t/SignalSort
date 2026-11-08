using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SignalSort.Infrastructure.Updates;

namespace SignalSort.Tests;

public class HttpsManifestSourceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public Uri? LastRequestUri { get; private set; }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    [Fact]
    public void Constructor_HttpUrl_Rejected()
    {
        Assert.Throws<ArgumentException>(
            () => new HttpsManifestSource(new Uri("http://example.com/manifest.json")));
    }

    [Fact]
    public void Constructor_HttpsUrl_Accepted()
    {
        var source = new HttpsManifestSource(new Uri("https://example.com/manifest.json"));

        Assert.NotNull(source);
    }

    [Fact]
    public async Task Fetch_ParsesManifest()
    {
        const string json = """
            {
              "schemaVersion": "1",
              "channel": "stable",
              "updates": [
                {
                  "id": "app",
                  "name": "SignalSort",
                  "type": "Application",
                  "version": "1.2.0",
                  "minimumCompatibleVersion": "0.3.0",
                  "downloadUrl": "https://example.com/pkg.zip",
                  "sha256": "abcd"
                }
              ]
            }
            """;
        var handler = new StubHandler(_ => Json(json));
        using var client = new HttpClient(handler);
        var source = new HttpsManifestSource(new Uri("https://example.com/manifest.json"), client);

        var manifest = await source.FetchManifestAsync("stable");

        Assert.NotNull(manifest);
        Assert.Equal("1", manifest.SchemaVersion);
        var update = Assert.Single(manifest.Updates);
        Assert.Equal("app", update.Id);
        Assert.Equal(UpdateType.Application, update.Type);
        Assert.Equal("1.2.0", update.Version);
    }

    [Fact]
    public async Task Fetch_NonStableChannel_AppendsChannelQuery()
    {
        var handler = new StubHandler(_ => Json("""{"schemaVersion":"1"}"""));
        using var client = new HttpClient(handler);
        var source = new HttpsManifestSource(new Uri("https://example.com/manifest.json"), client);

        await source.FetchManifestAsync("beta");

        Assert.Equal("https://example.com/manifest.json?channel=beta", handler.LastRequestUri!.ToString());
    }

    [Fact]
    public async Task Fetch_NotFound_ReturnsNull()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(handler);
        var source = new HttpsManifestSource(new Uri("https://example.com/manifest.json"), client);

        var manifest = await source.FetchManifestAsync("stable");

        Assert.Null(manifest);
    }

    [Fact]
    public async Task Fetch_ServerError_Throws()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = new HttpClient(handler);
        var source = new HttpsManifestSource(new Uri("https://example.com/manifest.json"), client);

        await Assert.ThrowsAsync<HttpRequestException>(() => source.FetchManifestAsync("stable"));
    }
}
