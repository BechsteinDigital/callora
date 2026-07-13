using System.Collections.Concurrent;
using Callora.Host.Backend.Application.Abstractions.Plugins;

namespace Callora.Host.Backend.Application.Plugins;

/// <summary>
/// Thread-safe in-memory activation store for tests and hosts without database.
/// </summary>
public sealed class InMemoryWorkspacePluginActivationStore :
    IWorkspacePluginActivationStore,
    IWorkspacePluginActivationReader
{
    private readonly ConcurrentDictionary<string, bool> _activations = new(StringComparer.OrdinalIgnoreCase);

    public Task SetActiveAsync(
        string pluginId,
        string workspaceKey,
        string tenantKey,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        _activations[BuildKey(pluginId, workspaceKey)] = isActive;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListActivePluginIdsAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{workspaceKey.Trim()}|";
        IReadOnlyList<string> active = _activations
            .Where(pair => pair.Value && pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key[prefix.Length..])
            .ToArray();
        return Task.FromResult(active);
    }

    private static string BuildKey(string pluginId, string workspaceKey) =>
        $"{workspaceKey.Trim()}|{pluginId.Trim()}";
}
