using Callora.Core.Application.Events.Business;
using Callora.Core.Application.Flows;

namespace Callora.Administration.Api;

/// <summary>
/// Public shape of one flow rule.
/// </summary>
/// <remarks>
/// <see cref="MatchesKnownEvent"/> is derived per request, never stored. A rule may
/// legitimately be ahead of its plugin — a trigger on <c>communication.call.ringing</c>
/// before the plugin is installed is the normal order when preparing a workspace — while a
/// misspelling never fires at all. Without this flag the two look identical. Because it is
/// derived, it becomes true on its own once the plugin arrives.
/// </remarks>
public sealed record FlowApiResponse(
    Guid Id,
    string WorkspaceKey,
    string Name,
    string TriggerEvent,
    string? ConditionsJson,
    string ActionsJson,
    bool IsActive,
    int Priority,
    DateTimeOffset CreatedAtUtc,
    bool MatchesKnownEvent)
{
    /// <summary>Projects a stored flow, resolving the trigger against the events that exist.</summary>
    public static FlowApiResponse From(FlowSnapshot flow, IReadOnlyCollection<string> knownEvents) => new(
        flow.Id,
        flow.WorkspaceKey,
        flow.Name,
        flow.TriggerEvent,
        flow.ConditionsJson,
        flow.ActionsJson,
        flow.IsActive,
        flow.Priority,
        flow.CreatedAtUtc,
        BusinessEventPattern.MatchesAny(flow.TriggerEvent, knownEvents));
}
