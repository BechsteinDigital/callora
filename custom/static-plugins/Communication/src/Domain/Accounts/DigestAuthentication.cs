namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// SIP digest authentication: a username and a reference to the password in the secret store
/// (the password itself is never held here). Used by registering accounts and credentialed trunks.
/// </summary>
public sealed record DigestAuthentication : SipAuthentication
{
    /// <summary>Creates and validates digest credentials.</summary>
    public DigestAuthentication(string username, string? authId, string passwordSecretRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordSecretRef);

        Username = username;
        AuthId = authId;
        PasswordSecretRef = passwordSecretRef;
    }

    /// <inheritdoc />
    public override SipAuthMethod Method => SipAuthMethod.Digest;

    /// <summary>Authentication user name.</summary>
    public string Username { get; }

    /// <summary>Optional distinct authentication id (defaults to the user name when null).</summary>
    public string? AuthId { get; }

    /// <summary>Reference to the password in the secret store.</summary>
    public string PasswordSecretRef { get; }
}
