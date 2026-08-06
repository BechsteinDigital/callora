namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Request to place one outbound call. The channel is chosen by <see cref="ChannelId"/> when given,
/// otherwise the first voice-capable channel registered for the workspace is used.
/// </summary>
/// <param name="WorkspaceKey">Workspace that owns the call.</param>
/// <param name="To">Channel-neutral target address (e.g. a phone number "+49301234567").</param>
/// <param name="ChannelId">Optional explicit channel; null picks the first voice-capable channel.</param>
/// <param name="DisplayName">Optional human-readable name for the remote party.</param>
public sealed record PlaceCallCommand(
    string WorkspaceKey,
    string To,
    string? ChannelId = null,
    string? DisplayName = null,
    string? Origin = null)
{
    /// <summary>
    /// What is placing this call, for the line quota an operator may have configured — a plain name
    /// such as <c>crm</c>, or a finer one such as <c>dialer:campaign-x</c> when one consumer runs
    /// several things that should not exhaust each other.
    /// </summary>
    /// <remarks>
    /// Stated by the caller rather than derived. Plugins run trusted in-process (ADR-013), so a quota
    /// is an operating limit and not a security boundary: it keeps a busy consumer from taking every
    /// line by accident, which is the failure that actually happens. A consumer that misnames its own
    /// origin only misleads itself. Omitted means no quota applies.
    /// </remarks>
    public string? Origin { get; init; } = Origin;
}
