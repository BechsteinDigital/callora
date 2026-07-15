using Callora.Plugin.Communication.Abstractions;
using Callora.Core.Application.Events.Contracts;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Adapts one call stream event to the platform business-event bus
/// (PLAT-270), so flows, webhooks and other plugins react to calls through
/// the same generic mechanism as any other event.
/// </summary>
public sealed class CallBusinessEvent(CallStreamEvent callEvent) : IBusinessEvent
{
    public string EventName => callEvent.Type;

    public string? WorkspaceKey => callEvent.Call.WorkspaceKey;

    public IReadOnlyDictionary<string, string> ToEventData() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["callId"] = callEvent.Call.CallId,
        ["workspaceKey"] = callEvent.Call.WorkspaceKey,
        ["channelId"] = callEvent.Call.ChannelId,
        ["direction"] = callEvent.Call.Direction,
        ["state"] = callEvent.Call.State,
        ["target"] = callEvent.Call.TargetValue,
        ["targetDisplayName"] = callEvent.Call.TargetDisplayName ?? string.Empty
    };
}
