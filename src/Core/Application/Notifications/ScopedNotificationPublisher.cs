using Callora.Core.Application.Notifications;
using Callora.Core.Application.Notifications.Contracts;

namespace Callora.Core.Application.Notifications;

/// <summary>
/// Singleton facade over the scoped notification store so plugins and
/// singleton services can publish notifications.
/// </summary>
public sealed class ScopedNotificationPublisher(IServiceScopeFactory scopeFactory) : INotificationPublisher
{
    public async Task PublishAsync(
        string title,
        string message,
        string level = NotificationLevels.Info,
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<INotificationStore>();
        await store.AddAsync(workspaceKey, title, message, level, cancellationToken).ConfigureAwait(false);
    }
}
