namespace Callora.Host.Backend.Domain.Plugins;

/// <summary>
/// One JSON document stored on behalf of a plugin, scoped by plugin,
/// workspace and collection.
/// </summary>
public sealed class PluginDataDocument
{
    private PluginDataDocument()
    {
    }

    public Guid Id { get; private set; }

    public string PluginId { get; private set; } = string.Empty;

    /// <summary>Workspace scope; empty string represents plugin-global data.</summary>
    public string WorkspaceKey { get; private set; } = string.Empty;

    public string Collection { get; private set; } = string.Empty;

    public string EntryKey { get; private set; } = string.Empty;

    public string JsonDocument { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PluginDataDocument Create(
        string pluginId,
        string workspaceKey,
        string collection,
        string entryKey,
        string jsonDocument,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryKey);
        ArgumentNullException.ThrowIfNull(jsonDocument);

        return new PluginDataDocument
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            WorkspaceKey = workspaceKey,
            Collection = collection,
            EntryKey = entryKey,
            JsonDocument = jsonDocument,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void UpdateDocument(string jsonDocument, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(jsonDocument);

        JsonDocument = jsonDocument;
        UpdatedAtUtc = nowUtc;
    }
}
