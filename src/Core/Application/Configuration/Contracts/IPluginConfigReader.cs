namespace Callora.Core.Application.Configuration.Contracts;

/// <summary>
/// Read access to the effective system configuration for plugins. Values are
/// JSON-encoded strings resolved workspace &gt; tenant &gt; global &gt; default;
/// the typed helpers unwrap primitive JSON values.
/// </summary>
public interface IPluginConfigReader
{
    /// <summary>
    /// Returns all effective config values for the plugin, keyed by config key.
    /// </summary>
    Task<IReadOnlyDictionary<string, string?>> GetAllAsync(
        string pluginId,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the raw string value for the key, or null when it is unset.
    /// </summary>
    Task<string?> GetStringAsync(
        string pluginId,
        string configKey,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the value as a boolean, falling back to <paramref name="fallback"/>
    /// when the key is unset or not a JSON boolean.
    /// </summary>
    Task<bool> GetBoolAsync(
        string pluginId,
        string configKey,
        bool fallback = false,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the value as an integer, falling back to <paramref name="fallback"/>
    /// when the key is unset or not a JSON number.
    /// </summary>
    Task<int> GetIntAsync(
        string pluginId,
        string configKey,
        int fallback = 0,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);
}
