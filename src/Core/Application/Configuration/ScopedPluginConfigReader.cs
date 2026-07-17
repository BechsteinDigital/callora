using Callora.Core.Application.Configuration.Contracts;
using System.Text.Json;

namespace Callora.Core.Application.Configuration;

/// <summary>
/// Singleton facade over the scoped config resolver so plugins (which live in
/// the root container) can read configuration. Mirrors ScopedPluginDataStore.
/// </summary>
public sealed class ScopedPluginConfigReader(IServiceScopeFactory scopeFactory) : IPluginConfigReader
{
    public async Task<IReadOnlyDictionary<string, string?>> GetAllAsync(
        string pluginId,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<SystemConfigResolver>();
        return await resolver
            .ResolveAsync(pluginId, tenantKey: null, workspaceKey, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> GetStringAsync(
        string pluginId,
        string configKey,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        var json = await GetRawAsync(pluginId, configKey, workspaceKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    public async Task<bool> GetBoolAsync(
        string pluginId,
        string configKey,
        bool fallback = false,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        var json = await GetRawAsync(pluginId, configKey, workspaceKey, cancellationToken).ConfigureAwait(false);
        return json switch
        {
            null or "" => fallback,
            "true" or "\"true\"" => true,
            "false" or "\"false\"" => false,
            _ => fallback
        };
    }

    public async Task<int> GetIntAsync(
        string pluginId,
        string configKey,
        int fallback = 0,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        var json = await GetRawAsync(pluginId, configKey, workspaceKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        return int.TryParse(json.Trim('"'), out var value) ? value : fallback;
    }

    private async Task<string?> GetRawAsync(
        string pluginId,
        string configKey,
        string? workspaceKey,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<SystemConfigResolver>();
        return await resolver
            .ResolveValueAsync(pluginId, configKey, tenantKey: null, workspaceKey, cancellationToken)
            .ConfigureAwait(false);
    }
}
