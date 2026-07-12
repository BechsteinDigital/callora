using Callora.Host.Backend.Application.Abstractions.Webhooks;
using Callora.Host.Backend.Domain.Webhooks;
using Callora.Host.PluginContracts.Application.Secrets;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfWebhookSubscriptionStore(
    HostPersistenceDbContext dbContext,
    IPluginDataProtector dataProtector) : IWebhookSubscriptionStore
{
    private const string ProtectionScope = "callora-webhooks";

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

        var entities = await query
            .OrderBy(x => x.CreatedAtUtc)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(ToSnapshot).ToArray();
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
            Secret = dataProtector.Protect(ProtectionScope, secret),
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

    private WebhookSubscriptionSnapshot ToSnapshot(WebhookSubscription entity) => new(
        entity.Id,
        entity.WorkspaceKey,
        entity.EventName,
        entity.TargetUrl,
        UnprotectSecret(entity.Secret),
        entity.IsActive,
        entity.CreatedAtUtc);

    private string UnprotectSecret(string storedSecret) =>
        dataProtector.TryUnprotect(ProtectionScope, storedSecret, out var plaintext)
            ? plaintext
            : storedSecret; // Legacy-Klartext bleibt lesbar; Neuanlagen sind verschlüsselt.
}
