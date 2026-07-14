namespace Callora.Host.PluginContracts.Application.Events;

/// <summary>
/// A named business event that flows, webhooks and plugin subscribers can
/// hook into — the Callora counterpart of Shopware's FlowEventAware events.
/// Any subsystem or plugin publishes these through <c>IBusinessEventBus</c>;
/// the platform routes them to every interested consumer.
/// </summary>
public interface IBusinessEvent : IHostEvent
{
    /// <summary>
    /// Stable dotted event name, e.g. "workspace.created" or "call.ringing".
    /// Consumers subscribe by this name.
    /// </summary>
    string EventName { get; }

    /// <summary>
    /// Workspace the event belongs to; null for platform-wide events.
    /// Used to scope flow and webhook matching.
    /// </summary>
    string? WorkspaceKey { get; }

    /// <summary>
    /// Flat string projection of the event payload. Powers flow conditions,
    /// webhook payloads and mail templates — the fields listed here are the
    /// ones a rule or template can reference.
    /// </summary>
    IReadOnlyDictionary<string, string> ToEventData();
}
