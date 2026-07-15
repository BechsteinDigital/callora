using Callora.Plugin.Communication.Abstractions;
using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Describes the call business events for discovery (flow-builder, webhook
/// UI): which events the voice plugin publishes and which fields they carry.
/// </summary>
public sealed class CallBusinessEventProvider : IBusinessEventProvider
{
    private static readonly IReadOnlyList<BusinessEventField> CallFields =
    [
        new("callId", BusinessEventFieldType.Text, "Call ID"),
        new("workspaceKey", BusinessEventFieldType.Text, "Workspace"),
        new("channelId", BusinessEventFieldType.Text, "Channel"),
        new("direction", BusinessEventFieldType.Text, "Direction"),
        new("state", BusinessEventFieldType.Text, "State"),
        new("target", BusinessEventFieldType.Text, "Target number"),
        new("targetDisplayName", BusinessEventFieldType.Text, "Target name")
    ];

    public IReadOnlyList<BusinessEventDescriptor> GetDescriptors() =>
    [
        new(CallEventTypes.Ringing, "Call ringing", CallFields),
        new(CallEventTypes.Placed, "Call placed", CallFields),
        new(CallEventTypes.StateChanged, "Call state changed", CallFields),
        new(CallEventTypes.Ended, "Call ended", CallFields),
        new(CallEventTypes.ConsentGranted, "Recording consent granted", CallFields),
        new(CallEventTypes.ConsentDenied, "Recording consent denied", CallFields)
    ];
}
