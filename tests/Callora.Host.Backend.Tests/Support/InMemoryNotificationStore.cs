using Callora.Host.Backend.Application.Notifications;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Thread-safe in-memory notification store for tests.
/// </summary>
public sealed class InMemoryNotificationStore : INotificationStore
{
    private readonly object _syncLock = new();
    private readonly Dictionary<Guid, NotificationSnapshot> _notifications = [];

    public Task<NotificationSnapshot> AddAsync(
        string? workspaceKey,
        string title,
        string message,
        string level,
        CancellationToken cancellationToken = default)
    {
        return AddAsync(workspaceKey, title, message, level, DateTimeOffset.UtcNow);
    }

    public Task<NotificationSnapshot> AddAsync(
        string? workspaceKey,
        string title,
        string message,
        string level,
        DateTimeOffset createdAtUtc)
    {
        var snapshot = new NotificationSnapshot(
            Guid.NewGuid(),
            workspaceKey,
            title,
            message,
            level,
            IsRead: false,
            createdAtUtc);

        lock (_syncLock)
        {
            _notifications[snapshot.Id] = snapshot;
        }

        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<NotificationSnapshot>> ListAsync(
        string? workspaceKey,
        bool includeRead,
        int limit,
        CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            IReadOnlyList<NotificationSnapshot> result = _notifications.Values
                .Where(x => includeRead || !x.IsRead)
                .Where(x => string.IsNullOrWhiteSpace(workspaceKey) ||
                            x.WorkspaceKey is null ||
                            string.Equals(x.WorkspaceKey, workspaceKey.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(Math.Max(1, limit))
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<NotificationSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            return Task.FromResult(_notifications.GetValueOrDefault(id));
        }
    }

    public Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            if (!_notifications.TryGetValue(id, out var existing))
            {
                return Task.FromResult(false);
            }

            _notifications[id] = existing with { IsRead = true };
            return Task.FromResult(true);
        }
    }

    public Task<int> DeleteCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            var expiredIds = _notifications.Values
                .Where(x => x.CreatedAtUtc < cutoffUtc)
                .Select(x => x.Id)
                .ToArray();

            foreach (var id in expiredIds)
            {
                _notifications.Remove(id);
            }

            return Task.FromResult(expiredIds.Length);
        }
    }
}
