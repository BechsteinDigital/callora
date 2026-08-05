namespace Callora.Plugin.Communication.Application.Admin;

/// <summary>
/// Readiness payload of the Communication operator status route (#112).
/// <para>
/// This is a <em>readiness</em> answer, not a liveness one: it reports whether the plugin can
/// currently serve calls, which depends on things outside the process. Host liveness stays
/// separate and must never fail because a carrier is unreachable, otherwise an orchestrator
/// restarts a perfectly healthy process over someone else's outage.
/// </para>
/// </summary>
/// <param name="PluginId">The plugin identifier.</param>
/// <param name="Status">
/// Aggregate over <paramref name="Dependencies"/>: <c>ready</c> when every required dependency
/// is up, <c>degraded</c> when calls are still possible but something is impaired, and
/// <c>unavailable</c> when no call can be placed.
/// </param>
/// <param name="Dependencies">One entry per checked dependency, in a stable order.</param>
public sealed record CommunicationStatus(
    string PluginId,
    string Status,
    IReadOnlyList<CommunicationDependencyStatus> Dependencies);
