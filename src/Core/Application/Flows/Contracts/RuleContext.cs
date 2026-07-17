namespace Callora.Core.Application.Flows.Contracts;

/// <summary>
/// Evaluation context of one rule check: the triggering event, its workspace
/// and a flat data bag (e.g. call fields). Now is injected for testability of
/// time-based conditions.
/// </summary>
/// <param name="EventName">Name of the event that triggered the rule check.</param>
/// <param name="WorkspaceKey">Workspace the event occurred in; null for host-wide.</param>
/// <param name="Data">Flat data bag of event fields keyed by name.</param>
/// <param name="Now">Evaluation timestamp, injected for testable time conditions.</param>
public sealed record RuleContext(
    string EventName,
    string? WorkspaceKey,
    IReadOnlyDictionary<string, string> Data,
    DateTimeOffset Now);
