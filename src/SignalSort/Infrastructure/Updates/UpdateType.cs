namespace SignalSort.Infrastructure.Updates;

/// <summary>
/// The kind of update an update source can offer.
/// </summary>
public enum UpdateType
{
    /// <summary>
    /// The SignalSort application itself (executable and dependencies).
    /// Requires restart and a full application release.
    /// </summary>
    Application,

    /// <summary>
    /// Declarative content such as classification rules or naming presets.
    /// Can be updated independently of the application executable.
    /// </summary>
    ContentPack,

    /// <summary>
    /// Declarative integration metadata (endpoints, capabilities) for an
    /// external service. Connector code itself still ships with the app.
    /// </summary>
    Integration
}
