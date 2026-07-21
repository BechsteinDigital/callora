using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Streaming;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Validates the <c>connectToken</c> route value against a pending <see cref="MediaStreamSession"/>
/// before the host accepts the WebSocket. It consumes the token atomically (Pending → Active in one
/// guarded write), so one token authorizes exactly one connect even under a concurrent double-connect,
/// and hands the resolved <c>workspace/session</c> to the handler as the connection subject. Denials
/// are uniform — the host rejects the upgrade without a body.
/// </summary>
public sealed class MediaStreamConnectTokenAuthorizer(
    IMediaStreamSessionStore sessionStore,
    TimeSpan? tokenTimeToLive = null) : IWebSocketConnectAuthorizer
{
    /// <summary>Route-value name carrying the connect token (matches <c>media/{connectToken}</c>).</summary>
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

        var session = await sessionStore
            .TryActivateByConnectTokenAsync(token, DateTimeOffset.UtcNow, _tokenTimeToLive, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            // Unknown, expired or already-used — a single uniform denial, no oracle to the caller.
            return WebSocketConnectAuthorization.Deny("invalid connect token");
        }

        return WebSocketConnectAuthorization.Allow($"{session.WorkspaceKey}/{session.Id}");
    }
}
