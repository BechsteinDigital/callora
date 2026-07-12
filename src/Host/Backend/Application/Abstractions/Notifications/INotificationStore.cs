namespace Callora.Host.Backend.Application.Abstractions.Notifications;

public interface INotificationStore
{
    Task<NotificationSnapshot> AddAsync(
        string? workspaceKey,
        string title,
        string message,
        string level,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationSnapshot>> ListAsync(
        string? workspaceKey,
        bool includeRead,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken = default);
}
