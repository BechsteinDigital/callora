namespace Callora.Host.PluginContracts.Application.Configuration;

/// <summary>
/// Read access to the effective system configuration for plugins. Values are
/// JSON-encoded strings resolved workspace &gt; tenant &gt; global &gt; default;
/// the typed helpers unwrap primitive JSON values.
/// </summary>
public interface IPluginConfigReader
{
    Task<IReadOnlyDictionary<string, string?>> GetAllAsync(
        string pluginId,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);

    Task<string?> GetStringAsync(
        string pluginId,
        string configKey,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);

    Task<bool> GetBoolAsync(
        string pluginId,
        string configKey,
        bool fallback = false,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);

    Task<int> GetIntAsync(
        string pluginId,
        string configKey,
        int fallback = 0,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);
}
