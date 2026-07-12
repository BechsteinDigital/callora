namespace Callora.Host.PluginContracts.Application.Notifications;

/// <summary>
/// Publishes in-app notifications to the admin notification center. Available
/// to plugins as host service.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(
        string title,
        string message,
        string level = NotificationLevels.Info,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);
}
