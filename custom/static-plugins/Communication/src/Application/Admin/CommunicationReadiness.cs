namespace Callora.Plugin.Communication.Application.Admin;

/// <summary>
/// The aggregate verdicts of the Communication readiness probe (#112). Constants rather than
/// an enum, because they are wire values a monitor matches on and must not shift with a
/// serializer setting.
/// </summary>
public static class CommunicationReadiness
{
    /// <summary>Every configured dependency is up; calls can be placed.</summary>
    public const string Ready = "ready";

    /// <summary>Calls are still possible, but a dependency is impaired.</summary>
    public const string Degraded = "degraded";

    /// <summary>No call can be placed.</summary>
    public const string Unavailable = "unavailable";

    /// <summary>
    /// The plugin is running its existing calls dry before a stop (ADR-018 §2.1). Reported
    /// separately from <see cref="Unavailable"/> because nothing is broken: calls still up are
    /// carried to their end, only new ones are refused. A monitor should read this as a planned
    /// state, not an outage.
    /// </summary>
    public const string Draining = "draining";
}
