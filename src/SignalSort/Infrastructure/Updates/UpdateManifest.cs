using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SignalSort.Infrastructure.Updates;

/// <summary>
/// Top-level update manifest retrieved from an update source.
/// </summary>
public class UpdateManifest
{
    public string SchemaVersion { get; set; } = "1";
    public string Channel { get; set; } = "stable";
    public List<UpdateInfo> Updates { get; set; } = new();
}
