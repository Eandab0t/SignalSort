using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SignalSort.Infrastructure.Updates;

/// <summary>
/// A single update offered by an update source.
/// </summary>
public class UpdateInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UpdateType Type { get; set; } = UpdateType.Application;
    public string Version { get; set; } = "0.0.0";
    public string MinimumCompatibleVersion { get; set; } = "0.0.0";
    public string Description { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
    public bool RestartRequired { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}
