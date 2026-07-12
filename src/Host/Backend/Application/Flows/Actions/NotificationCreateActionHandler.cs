using Callora.Host.PluginContracts.Application.Flows;
using Callora.Host.PluginContracts.Application.Notifications;

namespace Callora.Host.Backend.Application.Flows.Actions;

/// <summary>Creates an in-app notification ("title", "message", "level").</summary>
public sealed class NotificationCreateActionHandler(INotificationPublisher publisher) : IFlowActionHandler
{
    public string Type => "notification.create";

    public Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default) =>
        publisher.PublishAsync(
            parameters.GetValueOrDefault("title", $"Flow-Ereignis: {context.EventName}"),
            parameters.GetValueOrDefault("message", string.Empty),
            parameters.GetValueOrDefault("level", NotificationLevels.Info),
            context.WorkspaceKey,
            cancellationToken);
}
