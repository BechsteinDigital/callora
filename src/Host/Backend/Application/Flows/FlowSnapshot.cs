namespace Callora.Host.Backend.Application.Flows;

public sealed record FlowSnapshot(
    Guid Id,
    string WorkspaceKey,
    string Name,
    string TriggerEvent,
    string? ConditionsJson,
    string ActionsJson,
    bool IsActive,
    int Priority,
    DateTimeOffset CreatedAtUtc);
