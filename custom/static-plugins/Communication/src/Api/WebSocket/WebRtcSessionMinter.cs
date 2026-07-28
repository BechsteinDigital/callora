using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Sdk;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// The concrete <see cref="IWebRtcSessionMinter"/>: ties the channel provisioner (which gets-or-creates
/// the workspace <see cref="Infrastructure.Sdk.WebRtcVoiceChannel"/>) to the in-memory
/// <see cref="WebRtcSignalingSessionStore"/> (which mints the single-use connect-token).
/// The resulting <see cref="WebRtcSessionTicket"/> carries the token and its advisory TTL so the
/// browser can initiate a WebSocket connect before the window closes.
/// </summary>
internal sealed class WebRtcSessionMinter : IWebRtcSessionMinter
{
    private readonly WebRtcChannelProvisioner _provisioner;
    private readonly WebRtcSignalingSessionStore _store;
    private readonly TimeSpan _tokenTimeToLive;

    /// <param name="provisioner">Gets or creates the workspace voice channel.</param>
    /// <param name="store">Mints and stores the connect-token.</param>
    /// <param name="tokenTimeToLive">
    /// Lifetime passed to the store at mint time (advisory for the browser); the signalling
    /// authorizer enforces the same window server-side. Should match the authorizer's default
    /// TTL (2 minutes).
    /// </param>
    public WebRtcSessionMinter(
        WebRtcChannelProvisioner provisioner,
        WebRtcSignalingSessionStore store,
        TimeSpan tokenTimeToLive)
    {
        ArgumentNullException.ThrowIfNull(provisioner);
        ArgumentNullException.ThrowIfNull(store);

        _provisioner = provisioner;
        _store = store;
        _tokenTimeToLive = tokenTimeToLive;
    }

    /// <inheritdoc />
    public WebRtcSessionTicket MintSession(string workspaceKey, CallTarget target, string? callId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(target);

        var effectiveCallId = string.IsNullOrWhiteSpace(callId)
            ? Guid.NewGuid().ToString("n")
            : callId;

        var channel = _provisioner.GetOrCreateChannel(workspaceKey);
        var session = new WebRtcSignalingSession(_provisioner.Client, channel, effectiveCallId, target);
        var token = _store.Mint(session);

        return new WebRtcSessionTicket(token, (int)_tokenTimeToLive.TotalSeconds);
    }
}
