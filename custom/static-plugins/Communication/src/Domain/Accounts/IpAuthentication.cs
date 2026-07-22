namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// IP-based authentication: the registrar/trunk trusts the source address, so no credentials are
/// held at all. This is the classic IP-authenticated SIP trunk that must not be forced to carry a
/// username, password or registration expiry.
/// </summary>
public sealed record IpAuthentication : SipAuthentication
{
    /// <summary>The shared instance — IP authentication carries no state.</summary>
    public static IpAuthentication Instance { get; } = new();

    /// <inheritdoc />
    public override SipAuthMethod Method => SipAuthMethod.IpAuthenticated;
}
