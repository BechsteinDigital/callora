namespace Callora.Core.Application.Notifications.Contracts;

/// <summary>
/// Publishes in-app notifications to the admin notification center. Available
/// to plugins as host service.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes one notification to the admin notification center.
    /// <paramref name="level"/> is a value from <see cref="NotificationLevels"/>.
    /// </summary>
    Task PublishAsync(
        string title,
        string message,
        string level = NotificationLevels.Info,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);
}
