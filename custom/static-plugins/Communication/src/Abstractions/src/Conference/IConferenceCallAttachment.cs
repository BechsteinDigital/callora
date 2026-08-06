namespace Callora.Plugin.Communication.Abstractions.Conference;

/// <summary>
/// Hangs a live call into a conference, so that somebody on an ordinary telephone takes part in a room
/// full of browsers.
/// </summary>
/// <remarks>
/// <para>This lives here because it cannot live anywhere else. A conference hands out an offer and
/// expects an answer, and producing one means terminating media — DTLS, SRTP, an encoder. A plugin
/// without a media engine can pass an offer on to a browser, which is what a video conference does,
/// but behind a telephone there is no browser. The second half is the same story: the conference
/// forwards encoded streams because browsers mix locally, while a phone receives a single stream and
/// mixes nothing, so for that one participant the server has to decode, mix and re-encode. Both halves
/// need what only this plugin has.</para>
/// </remarks>
public interface IConferenceCallAttachment
{
    /// <summary>
    /// Attaches the call <paramref name="callId"/> to the conference <paramref name="conferenceId"/>,
    /// where it appears as <paramref name="participantId"/> — an ordinary participant to the other
    /// members, who neither know nor need to know that a telephone is on the other end.
    /// </summary>
    /// <param name="workspaceKey">The workspace the caller acts for; the call must belong to it.</param>
    /// <param name="callId">The live call to attach.</param>
    /// <param name="conferenceId">The conference to attach it to.</param>
    /// <param name="participantId">The identity the call takes in the conference.</param>
    /// <param name="cancellationToken">Cancels the attach.</param>
    /// <exception cref="InvalidOperationException">
    /// The workspace has no such active call, the call carries no audio, the conference requires
    /// end-to-end encryption (which a bridged telephone cannot have), or the conference is not hosted
    /// on this node.
    /// </exception>
    Task<IConferenceCallLeg> AttachAsync(
        string workspaceKey,
        string callId,
        string conferenceId,
        string participantId,
        CancellationToken cancellationToken = default);
}
