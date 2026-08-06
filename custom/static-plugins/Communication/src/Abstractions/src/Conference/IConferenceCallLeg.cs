namespace Callora.Plugin.Communication.Abstractions.Conference;

/// <summary>
/// A call's membership in a conference, for as long as it lasts.
/// </summary>
/// <remarks>
/// <b>Disposing this ends the membership, not the call.</b> The two have separate lifetimes on
/// purpose: a caller is moved out of a lobby into a room, or out of a room that has closed, and stays
/// on the line throughout. Ending the call is a separate decision, made by whoever owns the call.
/// </remarks>
public interface IConferenceCallLeg : IAsyncDisposable
{
    /// <summary>Whether this leg's audio is currently withheld from the conference.</summary>
    bool IsMuted { get; }

    /// <summary>
    /// Withholds this leg's audio from the conference, or lets it through again.
    /// </summary>
    /// <remarks>
    /// Enforced on the server, never asked of the telephone: a host's force-mute must not depend on a
    /// device that has no way to honour it. The contribution simply does not reach the other
    /// participants.
    /// </remarks>
    Task SetMutedAsync(bool muted, CancellationToken cancellationToken = default);
}
