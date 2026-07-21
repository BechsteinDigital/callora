using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Streaming;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Validates the <c>connectToken</c> route value against a pending <see cref="MediaStreamSession"/>
/// before the host accepts the WebSocket. On success it consumes the token (Pending → Active), so
/// one token authorizes exactly one connect, and hands the resolved <c>workspace/session</c> to the
/// handler as the connection subject. Denials are uniform — the host rejects the upgrade without a
/// body.
/// </summary>
/// <remarks>
/// Single-use is enforced at the domain level (only a Pending session activates). Making a
/// simultaneous double-connect atomic (optimistic concurrency on the store update) is a hardening
/// follow-up (B4a-3).
/// </remarks>
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

        var session = await sessionStore.GetByConnectTokenAsync(token, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return WebSocketConnectAuthorization.Deny("unknown connect token");
        }

        var now = DateTimeOffset.UtcNow;
        if (!session.CanActivate(now, _tokenTimeToLive))
        {
            return WebSocketConnectAuthorization.Deny("connect token expired or already used");
        }

        session.Activate(now);
        await sessionStore.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        return WebSocketConnectAuthorization.Allow($"{session.WorkspaceKey}/{session.Id}");
    }
}
