namespace Callora.Core.Application.Flows;

/// <summary>Job payload for one matched flow execution.</summary>
public sealed record FlowExecutePayload(
    Guid FlowId,
    string EventName,
    string? WorkspaceKey,
    Dictionary<string, string> Data);
