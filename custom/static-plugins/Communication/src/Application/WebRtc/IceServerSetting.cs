namespace Callora.Plugin.Communication.Application.WebRtc;

/// <summary>
/// One configured STUN/TURN server as the deployment declares it, before per-session credentials
/// are derived.
/// </summary>
/// <param name="Url">
/// ICE URL in RFC 7064/7065 form, for example <c>stun:stun.example.com:3478</c> or
/// <c>turns:turn.example.com:5349?transport=tcp</c>.
/// </param>
/// <param name="SharedSecret">
/// The TURN server's REST-API secret (coturn <c>static-auth-secret</c>). When present, every session
/// gets its own short-lived credential derived from it and no long-lived password is handed to a
/// browser. When absent, <paramref name="Username"/> and <paramref name="Credential"/> are passed
/// through as configured.
/// </param>
/// <param name="Username">Static username, used only without a shared secret.</param>
/// <param name="Credential">Static credential, used only without a shared secret.</param>
public sealed record IceServerSetting(
    string Url,
    string? SharedSecret = null,
    string? Username = null,
    string? Credential = null)
{
    /// <summary>Whether this server issues per-session credentials rather than a static password.</summary>
    public bool IssuesShortLivedCredentials => !string.IsNullOrWhiteSpace(SharedSecret);
}
