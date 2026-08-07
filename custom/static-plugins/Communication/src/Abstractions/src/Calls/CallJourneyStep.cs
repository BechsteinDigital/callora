namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// One thing that happened to a call, in the order it happened.
/// </summary>
/// <remarks>
/// <para>A call history row says a call ended; it never says why it went where it went. This is the
/// missing half: a caller who reached nobody leaves a trail an operator can read instead of three
/// plugins' log files, if any of them logged at all.</para>
/// <para><b>Nothing secret goes in here.</b> A step is shown to whoever may look at the call. A typed
/// PIN, a token or a credential belongs in none of these fields — <see cref="Detail"/> is for what a
/// human needs to understand the step, not for what the machine passed around.</para>
/// </remarks>
public sealed record CallJourneyStep
{
    /// <summary>Records one step.</summary>
    /// <param name="source">
    /// Who recorded it — a plugin id such as <c>communication</c> or <c>videoconference</c>. Without
    /// it the journey stops being readable as a story.
    /// </param>
    /// <param name="step">
    /// What happened, as a short stable name: <c>call.ringing</c>, <c>dial-in.claimed</c>,
    /// <c>room.attached</c>. Stable because an operator learns to recognise it and a filter matches on
    /// it.
    /// </param>
    /// <param name="detail">One sentence a human can act on, or nothing.</param>
    public CallJourneyStep(string source, string step, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(step);

        Source = source.Trim();
        Step = step.Trim();
        Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
    }

    /// <summary>Plugin that recorded the step.</summary>
    public string Source { get; }

    /// <summary>Short stable name of what happened.</summary>
    public string Step { get; }

    /// <summary>Optional human-readable explanation.</summary>
    public string? Detail { get; }

    /// <summary>
    /// When it happened. Stamped by the journey when the step is recorded, not by the caller — the
    /// order of a call's steps must not depend on which thread got to a clock first.
    /// </summary>
    public DateTimeOffset At { get; init; }
}
