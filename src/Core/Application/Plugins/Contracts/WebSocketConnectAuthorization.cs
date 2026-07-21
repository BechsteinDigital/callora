namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Outcome of an <see cref="IWebSocketConnectAuthorizer"/> decision. On success the
/// authorizer may carry a resolved <see cref="Subject"/> (for example the workspace
/// or session principal derived from the connect-token) forward onto the accepted
/// <see cref="HostWebSocketConnection"/>. Denials are surfaced to the client as a
/// uniform handshake rejection; <see cref="FailureReason"/> is for host-side logging
/// only and is never sent to the caller.
/// </summary>
/// <param name="IsAuthorized">Whether the connection may be accepted.</param>
/// <param name="Subject">Optional principal identifier flowed onto the accepted connection.</param>
/// <param name="FailureReason">Optional host-side reason for a denial (not exposed to the caller).</param>
public sealed record WebSocketConnectAuthorization(
    bool IsAuthorized,
    string? Subject = null,
    string? FailureReason = null)
{
    /// <summary>Authorizes the connection, optionally carrying a resolved subject.</summary>
    public static WebSocketConnectAuthorization Allow(string? subject = null) => new(true, subject);

    /// <summary>Denies the connection with an optional host-side reason.</summary>
    public static WebSocketConnectAuthorization Deny(string? reason = null) => new(false, null, reason);
}
