using System.Collections.Concurrent;
using Callora.Host.Backend.Application.Abstractions.Extensions;

namespace Callora.Host.Backend.Infrastructure.Extensions;

public sealed class InMemoryWorkspaceTemplateRegistryStore : IWorkspaceTemplateRegistryStore
{
    private readonly ConcurrentDictionary<string, WorkspaceTemplateDefinitionSnapshot> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<WorkspaceTemplateDefinitionSnapshot>> ListDefinitionsAsync(
        string? surface = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<WorkspaceTemplateDefinitionSnapshot> query = _definitions.Values;
        if (!string.IsNullOrWhiteSpace(surface))
        {
            query = query.Where(x =>
                string.Equals(x.Surface, surface.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        return Task.FromResult<IReadOnlyList<WorkspaceTemplateDefinitionSnapshot>>(query
            .OrderBy(x => x.TemplateKey, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(x => x.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    public Task<WorkspaceTemplateDefinitionSnapshot> UpsertDefinitionAsync(
        string templateKey,
        string surface,
        string pluginId,
        string version,
        string displayName,
        string templatePath,
        string? parentTemplateKey,
        string scope,
        bool isActive,
        int priority,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var key = BuildDefinitionKey(templateKey, surface, pluginId, version);
        var nowUtc = DateTimeOffset.UtcNow;

        var result = _definitions.AddOrUpdate(
            key,
            _ => new WorkspaceTemplateDefinitionSnapshot(
                templateKey.Trim(),
                surface.Trim().ToLowerInvariant(),
                pluginId.Trim(),
                version.Trim(),
                displayName.Trim(),
                templatePath.Trim(),
                string.IsNullOrWhiteSpace(parentTemplateKey) ? null : parentTemplateKey.Trim(),
                scope.Trim().ToLowerInvariant(),
                isActive,
                priority,
                nowUtc,
                nowUtc),
            (_, existing) => existing with
            {
                DisplayName = displayName.Trim(),
                TemplatePath = templatePath.Trim(),
                ParentTemplateKey = string.IsNullOrWhiteSpace(parentTemplateKey) ? null : parentTemplateKey.Trim(),
                Scope = scope.Trim().ToLowerInvariant(),
                IsActive = isActive,
                Priority = priority,
                UpdatedAtUtc = nowUtc
            });

        return Task.FromResult(result);
    }

    public Task<bool> SetDefinitionActivationAsync(
        string templateKey,
        string pluginId,
        string version,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(templateKey) || string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(version))
        {
            return Task.FromResult(false);
        }

        var normalizedTemplateKey = templateKey.Trim();
        var normalizedPluginId = pluginId.Trim();
        var normalizedVersion = version.Trim();

        var updated = false;
        foreach (var pair in _definitions)
        {
            if (!string.Equals(pair.Value.TemplateKey, normalizedTemplateKey, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(pair.Value.PluginId, normalizedPluginId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(pair.Value.Version, normalizedVersion, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _definitions[pair.Key] = pair.Value with
            {
                IsActive = isActive,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            updated = true;
        }

        return Task.FromResult(updated);
    }

    public Task<IReadOnlyList<WorkspaceTemplateDefinitionSnapshot>> ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<WorkspaceTemplateDefinitionInput> definitions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(definitions);

        var normalizedPluginId = pluginId.Trim();
        var normalizedVersion = version.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        var keysToRemove = _definitions
            .Where(x =>
                string.Equals(x.Value.PluginId, normalizedPluginId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Value.Version, normalizedVersion, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Key)
            .ToArray();

        foreach (var key in keysToRemove)
        {
            _definitions.TryRemove(key, out _);
        }

        var inserted = new List<WorkspaceTemplateDefinitionSnapshot>(definitions.Count);
        foreach (var definition in definitions)
        {
            var snapshot = new WorkspaceTemplateDefinitionSnapshot(
                definition.TemplateKey.Trim(),
                definition.Surface.Trim().ToLowerInvariant(),
                normalizedPluginId,
                normalizedVersion,
                definition.DisplayName.Trim(),
                definition.TemplatePath.Trim(),
                string.IsNullOrWhiteSpace(definition.ParentTemplateKey) ? null : definition.ParentTemplateKey.Trim(),
                definition.Scope.Trim().ToLowerInvariant(),
                definition.IsActive,
                definition.Priority,
                nowUtc,
                nowUtc);
            _definitions[BuildDefinitionKey(
                snapshot.TemplateKey,
                snapshot.Surface,
                snapshot.PluginId,
                snapshot.Version)] = snapshot;
            inserted.Add(snapshot);
        }

        return Task.FromResult<IReadOnlyList<WorkspaceTemplateDefinitionSnapshot>>(inserted);
    }

    private static string BuildDefinitionKey(string templateKey, string surface, string pluginId, string version)
    {
        return string.Join(
            "::",
            templateKey.Trim(),
            surface.Trim().ToLowerInvariant(),
            pluginId.Trim(),
            version.Trim());
    }
}
