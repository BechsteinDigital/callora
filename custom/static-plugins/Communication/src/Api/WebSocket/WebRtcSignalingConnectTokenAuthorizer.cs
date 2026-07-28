using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Validates the <c>connectToken</c> route value against a pending WebRTC signalling session before the
/// host accepts the socket, mirroring <see cref="MediaStreamConnectTokenAuthorizer"/> on the media path.
/// It consumes the token atomically (valid → used in one guarded write) so one token authorizes exactly
/// one connect even under a concurrent double-connect, and hands the resolved subject to the handler as
/// the connection subject. Denials are uniform — the host rejects the upgrade without a body.
/// </summary>
public sealed class WebRtcSignalingConnectTokenAuthorizer(
    IWebRtcSignalingTokenStore tokenStore,
    TimeSpan? tokenTimeToLive = null) : IWebSocketConnectAuthorizer
{
    /// <summary>Route-value name carrying the connect token (matches <c>webrtc/{connectToken}</c>).</summary>
    public const string ConnectTokenRouteValue = "connectToken";

    private readonly TimeSpan _tokenTimeToLive = tokenTimeToLive ?? TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public async ValueTask<WebSocketConnectAuthorization> AuthorizeAsync(
        HostWebSocketConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.RouteValues.TryGetValue(ConnectTokenRouteValue, out var token) || string.IsNullOrWhiteSpace(token))
        {
            return WebSocketConnectAuthorization.Deny("missing connect token");
        }

        var subject = await tokenStore
            .TryConsumeAsync(token, DateTimeOffset.UtcNow, _tokenTimeToLive, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrEmpty(subject))
        {
            // Unknown, expired or already-used — a single uniform denial, no oracle to the caller.
            return WebSocketConnectAuthorization.Deny("invalid connect token");
        }

        return WebSocketConnectAuthorization.Allow(subject);
    }
}
