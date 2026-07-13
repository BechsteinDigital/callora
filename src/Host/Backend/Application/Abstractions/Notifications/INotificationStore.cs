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

    Task<NotificationSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes notifications created before the cutoff and returns the
    /// number of removed rows (retention, PLAT-240).
    /// </summary>
    Task<int> DeleteCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);
}
