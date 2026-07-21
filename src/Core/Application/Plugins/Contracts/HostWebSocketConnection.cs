using System.Net.WebSockets;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// An accepted plugin WebSocket. It hands the plugin the raw duplex
/// <see cref="System.Net.WebSockets.WebSocket"/> — the platform stays out of the
/// framing so a plugin can stream media (Twilio-Media-Streams-style) or any custom
/// protocol — together with the validated connect context and the
/// <see cref="Subject"/> resolved by the route's authorizer. The host owns the
/// socket lifetime and disposes it once the handler returns.
/// </summary>
public sealed class HostWebSocketConnection
{
    /// <summary>Creates an accepted connection.</summary>
    /// <param name="socket">The accepted duplex socket.</param>
    /// <param name="request">The connect context that produced this socket.</param>
    /// <param name="subject">The principal resolved by the authorizer, if any.</param>
    public HostWebSocketConnection(WebSocket socket, HostWebSocketConnectRequest request, string? subject)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(request);
        Socket = socket;
        Request = request;
        Subject = subject;
    }

    /// <summary>The accepted duplex socket. The host disposes it after the handler completes.</summary>
    public WebSocket Socket { get; }

    /// <summary>The connect context (plugin, route path, route values, query, sub-protocols).</summary>
    public HostWebSocketConnectRequest Request { get; }

    /// <summary>The principal resolved by the route's authorizer, or <see langword="null"/>.</summary>
    public string? Subject { get; }
}
