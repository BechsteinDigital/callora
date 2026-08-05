using System.Text.RegularExpressions;

namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// Makes a provider error safe to persist and show to an operator (#112).
/// <para>
/// A registration failure arrives from the SIP stack and can carry the request line that
/// caused it, which is where credentials live: <c>sip:alice:s3cret@host</c>, an
/// <c>Authorization: Digest …</c> header, or a bare token. The status field is readable by
/// anyone holding <c>communication.accounts.read</c> and ends up in logs, so it must not
/// become a second, unprotected copy of the secret store.
/// </para>
/// <para>
/// The patterns are compiled once and bounded with a timeout. Input here is attacker-adjacent
/// (a hostile registrar controls part of the message), so a pathological string must fail the
/// match rather than hang the callback that reports a lost registration.
/// </para>
/// </summary>
public static class SipStatusError
{
    /// <summary>
    /// Longest error kept. An operator needs the reason, not a stack trace, and an unbounded
    /// provider string would otherwise flow straight into a database column.
    /// </summary>
    public const int MaxLength = 500;

    private const string Placeholder = "***";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>Userinfo of a SIP URI: the password half of <c>sip:user:password@host</c>.</summary>
    private static readonly Regex SipUriCredentials = new(
        @"sip:([^\s:@]+):[^\s@]+@",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        MatchTimeout);

    /// <summary>A whole Authorization header value, digest or otherwise.</summary>
    private static readonly Regex AuthorizationHeader = new(
        @"Authorization:\s*\S.*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        MatchTimeout);

    /// <summary>Assignments whose name marks the value as a credential.</summary>
    private static readonly Regex CredentialAssignment = new(
        @"\b(password|passwd|pwd|secret|token|response|nonce)\s*=\s*""?[^\s""&,;]+""?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        MatchTimeout);

    /// <summary>
    /// Returns a redacted, length-bounded error, or null when there is nothing to report.
    /// </summary>
    public static string? Redact(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        var redacted = error.Trim();
        try
        {
            redacted = SipUriCredentials.Replace(redacted, $"sip:$1:{Placeholder}@");
            redacted = AuthorizationHeader.Replace(redacted, $"Authorization: {Placeholder}");
            redacted = CredentialAssignment.Replace(redacted, $"$1={Placeholder}");
        }
        catch (RegexMatchTimeoutException)
        {
            // Redaction could not complete, so the message cannot be shown to be safe.
            return "The provider reported an error that could not be safely redacted.";
        }

        return redacted.Length <= MaxLength
            ? redacted
            : string.Concat(redacted.AsSpan(0, MaxLength), "…");
    }
}
