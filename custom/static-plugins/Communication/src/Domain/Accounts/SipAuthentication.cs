namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// How a <see cref="SipConnection"/> proves its identity. A closed hierarchy (Replace Conditional
/// with Polymorphism) so a connection can only carry the fields its method actually needs — an
/// IP-authenticated trunk holds no username/password, a digest registration does. Concrete types:
/// <see cref="DigestAuthentication"/>, <see cref="IpAuthentication"/>, <see cref="MutualTlsAuthentication"/>.
/// </summary>
public abstract record SipAuthentication
{
    // Only this assembly's sealed subtypes may derive — the hierarchy is closed.
    private protected SipAuthentication()
    {
    }

    /// <summary>The authentication method this instance represents.</summary>
    public abstract SipAuthMethod Method { get; }
}
