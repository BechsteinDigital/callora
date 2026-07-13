using Callora.Host.Backend.Application.Abstractions.Notifications;
using Callora.Host.Backend.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfNotificationStore(HostPersistenceDbContext dbContext) : INotificationStore
{
    public async Task<NotificationSnapshot> AddAsync(
        string? workspaceKey,
        string title,
        string message,
        string level,
        CancellationToken cancellationToken = default)
    {
        var entity = new NotificationEntry
        {
            Id = Guid.NewGuid(),
            WorkspaceKey = string.IsNullOrWhiteSpace(workspaceKey) ? null : workspaceKey.Trim(),
            Title = title.Trim(),
            Message = message,
            Level = level,
            IsRead = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.Notifications.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(entity);
    }

    public async Task<IReadOnlyList<NotificationSnapshot>> ListAsync(
        string? workspaceKey,
        bool includeRead,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Notifications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(workspaceKey))
        {
            var normalized = workspaceKey.Trim();
            query = query.Where(x => x.WorkspaceKey == null || x.WorkspaceKey == normalized);
        }

        if (!includeRead)
        {
            query = query.Where(x => !x.IsRead);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(x => ToSnapshot(x))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<NotificationSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToSnapshot(entity);
    }

    public async Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Notifications
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        entity.IsRead = true;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<int> DeleteCreatedBeforeAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Notifications
            .Where(x => x.CreatedAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static NotificationSnapshot ToSnapshot(NotificationEntry entity) => new(
        entity.Id,
        entity.WorkspaceKey,
        entity.Title,
        entity.Message,
        entity.Level,
        entity.IsRead,
        entity.CreatedAtUtc);
}
