using Callora.Host.Backend.Application.Extensions;
using Callora.Host.Backend.Domain.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfWorkspaceTemplateRegistryStore(HostPersistenceDbContext dbContext) : IWorkspaceTemplateRegistryStore
{
    public async Task<IReadOnlyList<WorkspaceTemplateDefinitionSnapshot>> ListDefinitionsAsync(
        string? surface = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<WorkspaceTemplateDefinition>()
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(surface))
        {
            var normalizedSurface = surface.Trim().ToLowerInvariant();
            query = query.Where(x => x.Surface == normalizedSurface);
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(x => x.TemplateKey)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.PluginId)
            .ThenByDescending(x => x.Version)
            .Select(ToDefinitionSnapshotExpression())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WorkspaceTemplateDefinitionSnapshot> UpsertDefinitionAsync(
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
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var normalizedTemplateKey = templateKey.Trim();
        var normalizedSurface = surface.Trim().ToLowerInvariant();
        var normalizedPluginId = pluginId.Trim();
        var normalizedVersion = version.Trim();
        var normalizedDisplayName = displayName.Trim();
        var normalizedTemplatePath = templatePath.Trim();
        var normalizedParentTemplateKey = string.IsNullOrWhiteSpace(parentTemplateKey)
            ? null
            : parentTemplateKey.Trim();
        var normalizedScope = scope.Trim().ToLowerInvariant();
        var nowUtc = DateTimeOffset.UtcNow;

        var definition = await dbContext.Set<WorkspaceTemplateDefinition>()
            .SingleOrDefaultAsync(
                x => x.TemplateKey == normalizedTemplateKey &&
                     x.Surface == normalizedSurface &&
                     x.PluginId == normalizedPluginId &&
                     x.Version == normalizedVersion,
                cancellationToken)
            .ConfigureAwait(false);

        if (definition is null)
        {
            definition = new WorkspaceTemplateDefinition
            {
                Id = Guid.NewGuid(),
                TemplateKey = normalizedTemplateKey,
                Surface = normalizedSurface,
                PluginId = normalizedPluginId,
                Version = normalizedVersion,
                DisplayName = normalizedDisplayName,
                TemplatePath = normalizedTemplatePath,
                ParentTemplateKey = normalizedParentTemplateKey,
                Scope = normalizedScope,
                IsActive = isActive,
                Priority = priority,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
            };
            dbContext.Add(definition);
        }
        else
        {
            definition.DisplayName = normalizedDisplayName;
            definition.TemplatePath = normalizedTemplatePath;
            definition.ParentTemplateKey = normalizedParentTemplateKey;
            definition.Scope = normalizedScope;
            definition.IsActive = isActive;
            definition.Priority = priority;
            definition.UpdatedAtUtc = nowUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDefinitionSnapshot(definition);
    }

    public async Task<bool> SetDefinitionActivationAsync(
        string templateKey,
        string pluginId,
        string version,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateKey) ||
            string.IsNullOrWhiteSpace(pluginId) ||
            string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var normalizedTemplateKey = templateKey.Trim();
        var normalizedPluginId = pluginId.Trim();
        var normalizedVersion = version.Trim();

        var updated = await dbContext.Set<WorkspaceTemplateDefinition>()
            .Where(x =>
                x.TemplateKey == normalizedTemplateKey &&
                x.PluginId == normalizedPluginId &&
                x.Version == normalizedVersion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsActive, isActive)
                .SetProperty(x => x.UpdatedAtUtc, DateTimeOffset.UtcNow), cancellationToken)
            .ConfigureAwait(false);

        return updated > 0;
    }

    public async Task<IReadOnlyList<WorkspaceTemplateDefinitionSnapshot>> ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<WorkspaceTemplateDefinitionInput> definitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(definitions);

        var normalizedPluginId = pluginId.Trim();
        var normalizedVersion = version.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await dbContext.Set<WorkspaceTemplateDefinition>()
            .Where(x => x.PluginId == normalizedPluginId && x.Version == normalizedVersion)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var entities = definitions
            .Select(x => new WorkspaceTemplateDefinition
            {
                Id = Guid.NewGuid(),
                TemplateKey = x.TemplateKey.Trim(),
                Surface = x.Surface.Trim().ToLowerInvariant(),
                PluginId = normalizedPluginId,
                Version = normalizedVersion,
                DisplayName = x.DisplayName.Trim(),
                TemplatePath = x.TemplatePath.Trim(),
                ParentTemplateKey = string.IsNullOrWhiteSpace(x.ParentTemplateKey)
                    ? null
                    : x.ParentTemplateKey.Trim(),
                Scope = x.Scope.Trim().ToLowerInvariant(),
                IsActive = x.IsActive,
                Priority = x.Priority,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            })
            .ToArray();

        if (entities.Length > 0)
        {
            await dbContext.Set<WorkspaceTemplateDefinition>().AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(ToDefinitionSnapshot).ToArray();
    }

    private static WorkspaceTemplateDefinitionSnapshot ToDefinitionSnapshot(WorkspaceTemplateDefinition definition)
    {
        return new WorkspaceTemplateDefinitionSnapshot(
            definition.TemplateKey,
            definition.Surface,
            definition.PluginId,
            definition.Version,
            definition.DisplayName,
            definition.TemplatePath,
            definition.ParentTemplateKey,
            definition.Scope,
            definition.IsActive,
            definition.Priority,
            definition.CreatedAtUtc,
            definition.UpdatedAtUtc);
    }

    private static System.Linq.Expressions.Expression<Func<WorkspaceTemplateDefinition, WorkspaceTemplateDefinitionSnapshot>>
        ToDefinitionSnapshotExpression()
    {
        return x => new WorkspaceTemplateDefinitionSnapshot(
            x.TemplateKey,
            x.Surface,
            x.PluginId,
            x.Version,
            x.DisplayName,
            x.TemplatePath,
            x.ParentTemplateKey,
            x.Scope,
            x.IsActive,
            x.Priority,
            x.CreatedAtUtc,
            x.UpdatedAtUtc);
    }
}
