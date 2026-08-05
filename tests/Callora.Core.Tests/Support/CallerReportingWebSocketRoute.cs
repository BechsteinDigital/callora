using System.Net.WebSockets;
using System.Text;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;

namespace Callora.Core.Tests.Support;

/// <summary>
/// A WebSocket route that reports back what the host attached to the connect: the
/// caller's state and subject, or <c>none</c>. Doubles as authorizer and handler so a
/// test can see the caller at both points.
/// </summary>
public sealed class CallerReportingWebSocketRoute : IWebSocketConnectAuthorizer, IHostWebSocketHandler
{
    /// <summary>The caller the authorizer last saw on a connect request.</summary>
    public SurfaceCaller? LastCaller { get; private set; }

    public ValueTask<WebSocketConnectAuthorization> AuthorizeAsync(
        HostWebSocketConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        LastCaller = request.Caller;
        return ValueTask.FromResult(WebSocketConnectAuthorization.Allow(Describe(request.Caller)));
    }

    public async Task HandleAsync(
        HostWebSocketConnection connection,
        CancellationToken cancellationToken = default)
    {
        var reply = Encoding.UTF8.GetBytes(Describe(connection.Request.Caller));
        await connection.Socket
            .SendAsync(new ArraySegment<byte>(reply), WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
            .ConfigureAwait(false);
        await connection.Socket
            .CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", cancellationToken)
            .ConfigureAwait(false);
    }

    private static string Describe(SurfaceCaller? caller) => caller switch
    {
        AuthenticatedSurfaceCaller authenticated =>
            $"authenticated:{authenticated.Subject.Issuer}:{authenticated.Subject.SubjectId}",
        { } guest => $"guest:{guest.Subject.SubjectId}",
        _ => "none",
    };
}
