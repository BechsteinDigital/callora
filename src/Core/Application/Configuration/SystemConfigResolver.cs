using Callora.Core.Application.Configuration;

namespace Callora.Core.Application.Configuration;

/// <summary>
/// Resolves effective configuration values for one plugin: workspace values
/// override tenant values override global values override definition defaults.
/// </summary>
public sealed class SystemConfigResolver(ISystemConfigStore store)
{
    public async Task<IReadOnlyDictionary<string, string?>> ResolveAsync(
        string pluginId,
        string? tenantKey = null,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var definitions = await store
            .ListDefinitionsAsync(pluginId, cancellationToken)
            .ConfigureAwait(false);

        var effective = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions.Where(static definition => definition.IsActive))
        {
            effective[definition.ConfigKey] = definition.DefaultValueJson;
        }

        var scopeChain = BuildScopeChain(tenantKey, workspaceKey);
        var values = await store
            .ListValuesAsync(pluginId, scopeChain, cancellationToken)
            .ConfigureAwait(false);

        // Apply least-specific first so more specific scopes win.
        foreach (var (scope, scopeKey) in scopeChain)
        {
            // Ordinal wie im Store: Der Unique-Index unterscheidet Groß- und Kleinschreibung, der
            // Schreibpfad trimmt nur, und Workspace-Schlüssel werden nirgends kleingeschrieben.
            // Ein Vergleich, der sie ignoriert, macht aus zwei getrennten Workspaces einen.
            foreach (var value in values.Where(value =>
                         value.Scope == scope &&
                         string.Equals(value.ScopeKey, scopeKey, StringComparison.Ordinal)))
            {
                effective[value.ConfigKey] = value.ValueJson;
            }
        }

        return effective;
    }

    public async Task<string?> ResolveValueAsync(
        string pluginId,
        string configKey,
        string? tenantKey = null,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configKey);

        var effective = await ResolveAsync(pluginId, tenantKey, workspaceKey, cancellationToken).ConfigureAwait(false);
        return effective.TryGetValue(configKey.Trim(), out var value) ? value : null;
    }

    /// <summary>Scope chain ordered least specific → most specific.</summary>
    public static IReadOnlyList<(string Scope, string ScopeKey)> BuildScopeChain(
        string? tenantKey,
        string? workspaceKey)
    {
        var chain = new List<(string, string)> { (SystemConfigScopes.Global, string.Empty) };
        if (!string.IsNullOrWhiteSpace(tenantKey))
        {
            chain.Add((SystemConfigScopes.Tenant, tenantKey.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(workspaceKey))
        {
            chain.Add((SystemConfigScopes.Workspace, workspaceKey.Trim()));
        }

        return chain;
    }
}
