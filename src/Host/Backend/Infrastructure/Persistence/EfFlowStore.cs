using Callora.Host.Backend.Application.Flows;
using Callora.Host.Backend.Domain.Flows;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfFlowStore(HostPersistenceDbContext dbContext) : IFlowStore
{
    public async Task<IReadOnlyList<FlowSnapshot>> ListAsync(
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Flows.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(workspaceKey))
        {
            var normalized = workspaceKey.Trim();
            query = query.Where(x => x.WorkspaceKey == normalized);
        }

        return await query
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .Select(x => ToSnapshot(x))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FlowSnapshot>> ListActiveForTriggerAsync(
        string triggerEvent,
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedEvent = triggerEvent.Trim();
        var normalizedWorkspace = workspaceKey.Trim();
        return await dbContext.Flows
            .AsNoTracking()
            .Where(x => x.IsActive && x.TriggerEvent == normalizedEvent && x.WorkspaceKey == normalizedWorkspace)
            .OrderBy(x => x.Priority)
            .Select(x => ToSnapshot(x))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FlowSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Flows
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToSnapshot(entity);
    }

    public async Task<FlowSnapshot> UpsertAsync(FlowSnapshot flow, CancellationToken cancellationToken = default)
    {
        var entity = flow.Id == Guid.Empty
            ? null
            : await dbContext.Flows.FirstOrDefaultAsync(x => x.Id == flow.Id, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        if (entity is null)
        {
            entity = new FlowDefinition
            {
                Id = flow.Id == Guid.Empty ? Guid.NewGuid() : flow.Id,
                CreatedAtUtc = now
            };
            dbContext.Flows.Add(entity);
        }

        entity.WorkspaceKey = flow.WorkspaceKey.Trim();
        entity.Name = flow.Name.Trim();
        entity.TriggerEvent = flow.TriggerEvent.Trim();
        entity.ConditionsJson = flow.ConditionsJson;
        entity.ActionsJson = flow.ActionsJson;
        entity.IsActive = flow.IsActive;
        entity.Priority = flow.Priority;
        entity.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Flows
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        dbContext.Flows.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static FlowSnapshot ToSnapshot(FlowDefinition entity) => new(
        entity.Id,
        entity.WorkspaceKey,
        entity.Name,
        entity.TriggerEvent,
        entity.ConditionsJson,
        entity.ActionsJson,
        entity.IsActive,
        entity.Priority,
        entity.CreatedAtUtc);
}
