namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// The common payload of a plugin lifecycle report: which <paramref name="Action"/>
/// ran, the <paramref name="PluginId"/> it concerns, whether it succeeded, who
/// requested it, a human-readable message and optional metadata. Bundles the
/// parameter group shared by <see cref="PluginLifecycleReporter"/>'s report,
/// audit and event methods so callers pass one intent-revealing object instead
/// of five positional arguments.
/// </summary>
/// <param name="Action">The lifecycle action, e.g. <c>plugin.install</c>.</param>
/// <param name="PluginId">The plugin the action concerns, if any.</param>
/// <param name="IsSuccess">Whether the action succeeded.</param>
/// <param name="RequestedBy">The identity that requested the action, for audit.</param>
/// <param name="Message">A human-readable description of the outcome.</param>
/// <param name="Metadata">Optional structured metadata for the audit entry and event.</param>
public sealed record PluginLifecycleReport(
    string Action,
    string? PluginId,
    bool IsSuccess,
    string? RequestedBy,
    string? Message,
    IReadOnlyDictionary<string, string>? Metadata = null);
