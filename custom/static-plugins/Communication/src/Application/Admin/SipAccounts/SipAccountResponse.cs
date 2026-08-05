using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Operator-facing view of a <see cref="SipAccount"/>. Deliberately omits the credential: only the
/// digest username is shown, never the password (which lives protected in the secret store).
/// <para>
/// <c>Status</c>, <c>LastError</c>, <c>LastStatusChangeAt</c> and <c>LastRegisteredAt</c> come from
/// what the provider last reported (#112), so an operator can tell a never-connected account from
/// one that worked until a given moment. <c>LastError</c> is redacted in the domain.
/// </para>
/// </summary>
public sealed record SipAccountResponse(
    string Id,
    string WorkspaceKey,
    string DisplayName,
    string Host,
    int Port,
    string Transport,
    string Mode,
    string AuthMethod,
    string? Username,
    int? RegistrationExpirySeconds,
    string? OutboundProxy,
    IReadOnlyList<string> InboundNumbers,
    int MaxConcurrentCalls,
    bool Enabled,
    string Status,
    string? LastError,
    DateTimeOffset? LastStatusChangeAt,
    DateTimeOffset? LastRegisteredAt)
{
    /// <summary>Projects a domain account to its operator view (never exposes the password).</summary>
    public static SipAccountResponse FromDomain(SipAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var connection = account.Connection;
        var username = (connection.Authentication as DigestAuthentication)?.Username;

        return new SipAccountResponse(
            account.Id,
            account.WorkspaceKey,
            account.DisplayName,
            connection.Host,
            connection.Port,
            connection.Transport.ToString(),
            connection.Mode.ToString(),
            connection.Authentication.Method.ToString(),
            username,
            connection.RegistrationExpirySeconds,
            connection.OutboundProxy,
            connection.InboundNumbers,
            account.MaxConcurrentCalls,
            account.Enabled,
            account.Status.ToString(),
            account.LastError,
            account.LastStatusChangeAt,
            account.LastRegisteredAt);
    }
}
