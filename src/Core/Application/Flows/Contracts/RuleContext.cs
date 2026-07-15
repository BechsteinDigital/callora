namespace Callora.Core.Application.Flows.Contracts;

/// <summary>
/// Evaluation context of one rule check: the triggering event, its workspace
/// and a flat data bag (e.g. call fields). Now is injected for testability of
/// time-based conditions.
/// </summary>
public sealed record RuleContext(
    string EventName,
    string? WorkspaceKey,
    IReadOnlyDictionary<string, string> Data,
    DateTimeOffset Now);
