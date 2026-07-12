using Callora.Host.Backend.Application.Abstractions.Webhooks;
using Callora.Host.Backend.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfWebhookSubscriptionStore(HostPersistenceDbContext dbContext) : IWebhookSubscriptionStore
{
    public async Task<IReadOnlyList<WebhookSubscriptionSnapshot>> ListAsync(
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.WebhookSubscriptions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(workspaceKey))
        {
            var normalized = workspaceKey.Trim();
            query = query.Where(x => x.WorkspaceKey == normalized);
        }

        return await query
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => ToSnapshot(x))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WebhookSubscriptionSnapshot>> ListActiveForEventAsync(
        string eventName,
        string? workspaceKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedEvent = eventName.Trim();
        var normalizedWorkspace = workspaceKey?.Trim();

        return await dbContext.WebhookSubscriptions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Where(x => x.EventName == normalizedEvent || x.EventName == "*")
            .Where(x => x.WorkspaceKey == null || x.WorkspaceKey == normalizedWorkspace)
            .Select(x => ToSnapshot(x))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WebhookSubscriptionSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.WebhookSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToSnapshot(entity);
    }

    public async Task<WebhookSubscriptionSnapshot> CreateAsync(
        string? workspaceKey,
        string eventName,
        string targetUrl,
        string secret,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            WorkspaceKey = string.IsNullOrWhiteSpace(workspaceKey) ? null : workspaceKey.Trim(),
            EventName = eventName.Trim(),
            TargetUrl = targetUrl.Trim(),
            Secret = secret,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.WebhookSubscriptions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(entity);
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.WebhookSubscriptions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        entity.IsActive = isActive;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.WebhookSubscriptions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        dbContext.WebhookSubscriptions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static WebhookSubscriptionSnapshot ToSnapshot(WebhookSubscription entity) => new(
        entity.Id,
        entity.WorkspaceKey,
        entity.EventName,
        entity.TargetUrl,
        entity.Secret,
        entity.IsActive,
        entity.CreatedAtUtc);
}
