using System.Collections.Concurrent;
using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Application.Abstractions.Workspaces;
using Microsoft.Extensions.Caching.Memory;

namespace Callora.Host.Backend.Application.Extensions;

public sealed class CachedWorkspaceTemplateResolutionService(
    IMemoryCache cache,
    IServiceScopeFactory scopeFactory) : IWorkspaceTemplateResolutionService, IWorkspaceTemplateResolutionCache
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<string, string> _workspaceTenantIndex =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>>([]);
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var cacheKey = BuildCacheKey(normalizedWorkspaceKey);
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>? cached) &&
            cached is not null)
        {
            return Task.FromResult(cached);
        }

        return ResolveAndCacheAsync(cacheKey, normalizedWorkspaceKey, cancellationToken);
    }

    public void InvalidateWorkspace(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        cache.Remove(BuildCacheKey(normalizedWorkspaceKey));
        _workspaceTenantIndex.TryRemove(normalizedWorkspaceKey, out _);
    }

    public void InvalidateTenant(string tenantKey)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return;
        }

        var normalizedTenantKey = tenantKey.Trim();
        foreach (var pair in _workspaceTenantIndex)
        {
            if (!string.Equals(pair.Value, normalizedTenantKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            cache.Remove(BuildCacheKey(pair.Key));
            _workspaceTenantIndex.TryRemove(pair.Key, out _);
        }
    }

    public void InvalidateAll()
    {
        foreach (var workspaceKey in _workspaceTenantIndex.Keys)
        {
            cache.Remove(BuildCacheKey(workspaceKey));
        }

        _workspaceTenantIndex.Clear();
    }

    private async Task<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>> ResolveCoreAsync(
        string workspaceKey,
        CancellationToken cancellationToken)
    {
        // Scoped Stores pro Aufruf auflösen: Der Service ist Singleton (Cache-Index),
        // darf aber keine scoped Abhängigkeiten dauerhaft halten.
        using var scope = scopeFactory.CreateScope();
        var workspaceStore = scope.ServiceProvider.GetRequiredService<IWorkspaceManagementStore>();
        var templateRegistryStore = scope.ServiceProvider.GetRequiredService<IWorkspaceTemplateRegistryStore>();
        var entitlementStore = scope.ServiceProvider.GetRequiredService<IPluginEntitlementStore>();

        var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        if (workspace is null || !workspace.IsActive || !workspace.TenantIsActive)
        {
            return [];
        }

        _workspaceTenantIndex[workspace.WorkspaceKey] = workspace.TenantKey;

        if (string.IsNullOrWhiteSpace(workspace.ThemePluginId) || string.IsNullOrWhiteSpace(workspace.ThemeVersion))
        {
            return [];
        }

        var definitions = await templateRegistryStore
            .ListDefinitionsAsync(surface: "workspace", isActive: true, cancellationToken)
            .ConfigureAwait(false);

        if (definitions.Count == 0)
        {
            return [];
        }

        var entitledDefinitions = await FilterEntitledDefinitionsAsync(
                entitlementStore,
                workspace,
                definitions,
                cancellationToken)
            .ConfigureAwait(false);
        if (entitledDefinitions.Count == 0)
        {
            return [];
        }

        var themeDefinitions = entitledDefinitions
            .Where(x =>
                string.Equals(x.PluginId, workspace.ThemePluginId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Version, workspace.ThemeVersion, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.TemplateKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (themeDefinitions.Length == 0)
        {
            return [];
        }

        var definitionIndex = BuildDefinitionIndex(entitledDefinitions);
        var resolved = new List<WorkspaceTemplateEffectiveSnapshot>();
        var addedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in themeDefinitions)
        {
            AddResolvedTemplate(
                resolved,
                addedKeys,
                workspace,
                definition,
                source: "workspace-assigned");

            AppendInheritedChain(
                resolved,
                addedKeys,
                definition,
                definitionIndex,
                workspace,
                workspace.ThemePluginId,
                workspace.ThemeVersion);
        }

        return resolved;
    }

    private static string BuildCacheKey(string workspaceKey) =>
        $"workspace-template-resolution:{workspaceKey}";

    private async Task<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>> ResolveAndCacheAsync(
        string cacheKey,
        string workspaceKey,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveCoreAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        cache.Set(cacheKey, resolved, CacheDuration);
        return resolved;
    }

    private static void AddResolvedTemplate(
        ICollection<WorkspaceTemplateEffectiveSnapshot> destination,
        ISet<string> addedKeys,
        WorkspaceSnapshot workspace,
        WorkspaceTemplateDefinitionSnapshot definition,
        string source)
    {
        var identity = BuildIdentity(definition);
        if (!addedKeys.Add(identity))
        {
            return;
        }

        destination.Add(new WorkspaceTemplateEffectiveSnapshot(
            workspace.TenantKey,
            workspace.WorkspaceKey,
            definition.TemplateKey,
            definition.Surface,
            definition.PluginId,
            definition.Version,
            definition.DisplayName,
            definition.TemplatePath,
            definition.ParentTemplateKey,
            definition.Scope,
            source,
            definition.Priority));
    }

    private static string BuildIdentity(WorkspaceTemplateDefinitionSnapshot definition) =>
        $"{definition.TemplateKey.Trim()}::{definition.PluginId.Trim()}::{definition.Version.Trim()}";

    private static Dictionary<string, WorkspaceTemplateDefinitionSnapshot[]> BuildDefinitionIndex(
        IReadOnlyList<WorkspaceTemplateDefinitionSnapshot> definitions)
    {
        return definitions
            .GroupBy(x => x.TemplateKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.Priority)
                    .ThenBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static void AppendInheritedChain(
        ICollection<WorkspaceTemplateEffectiveSnapshot> destination,
        ISet<string> addedKeys,
        WorkspaceTemplateDefinitionSnapshot child,
        IReadOnlyDictionary<string, WorkspaceTemplateDefinitionSnapshot[]> definitionIndex,
        WorkspaceSnapshot workspace,
        string assignedPluginId,
        string assignedVersion)
    {
        var visitedTemplateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            child.TemplateKey
        };

        var current = child;
        while (!string.IsNullOrWhiteSpace(current.ParentTemplateKey))
        {
            var parentTemplateKey = current.ParentTemplateKey.Trim();
            if (!visitedTemplateKeys.Add(parentTemplateKey))
            {
                break;
            }

            if (!definitionIndex.TryGetValue(parentTemplateKey, out var candidates) || candidates.Length == 0)
            {
                break;
            }

            var parent = SelectBestParentCandidate(candidates, current, assignedPluginId, assignedVersion);
            if (parent is null)
            {
                break;
            }

            AddResolvedTemplate(
                destination,
                addedKeys,
                workspace,
                parent,
                source: "workspace-inherited");

            current = parent;
        }
    }

    private static WorkspaceTemplateDefinitionSnapshot? SelectBestParentCandidate(
        IReadOnlyList<WorkspaceTemplateDefinitionSnapshot> candidates,
        WorkspaceTemplateDefinitionSnapshot current,
        string assignedPluginId,
        string assignedVersion)
    {
        WorkspaceTemplateDefinitionSnapshot? best = null;
        var bestScore = int.MinValue;

        foreach (var candidate in candidates)
        {
            var score = 0;
            if (string.Equals(candidate.PluginId, current.PluginId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.Version, current.Version, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }
            else if (string.Equals(candidate.PluginId, assignedPluginId, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(candidate.Version, assignedVersion, StringComparison.OrdinalIgnoreCase))
            {
                score += 5;
            }

            score += candidate.Priority;

            if (best is null || score > bestScore)
            {
                best = candidate;
                bestScore = score;
                continue;
            }

            if (score == bestScore &&
                string.Compare(candidate.PluginId, best.PluginId, StringComparison.OrdinalIgnoreCase) < 0)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static async Task<IReadOnlyList<WorkspaceTemplateDefinitionSnapshot>> FilterEntitledDefinitionsAsync(
        IPluginEntitlementStore entitlementStore,
        WorkspaceSnapshot workspace,
        IReadOnlyList<WorkspaceTemplateDefinitionSnapshot> definitions,
        CancellationToken cancellationToken)
    {
        var pluginIds = definitions
            .Select(x => x.PluginId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (pluginIds.Length == 0)
        {
            return [];
        }

        var entitlementByPlugin = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var pluginId in pluginIds)
        {
            var entitled = await entitlementStore
                .IsEntitledAsync(pluginId, workspace.WorkspaceKey, workspace.TenantKey, cancellationToken)
                .ConfigureAwait(false);
            entitlementByPlugin[pluginId] = entitled;
        }

        return definitions
            .Where(x => entitlementByPlugin.TryGetValue(x.PluginId, out var entitled) && entitled)
            .ToArray();
    }
}
