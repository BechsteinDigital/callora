namespace Callora.Core.Application.Security;

/// <summary>
/// The revocation handle of a local account (#105). Every issued session carries the
/// stamp that was current at login; the request pipeline compares it against the
/// stored one, so rotating the stamp invalidates every outstanding session of that
/// account at once — password change, deactivation, deletion, RBAC change.
/// </summary>
public static class BackendSecurityStamp
{
    /// <summary>A fresh, unguessable stamp.</summary>
    public static string New() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Whether a session stamp still matches the account's. A stored stamp that is
    /// empty (an account written before stamps existed) matches nothing, so those
    /// sessions are treated as revoked rather than silently accepted.
    /// </summary>
    public static bool Matches(string? storedStamp, string? sessionStamp) =>
        !string.IsNullOrWhiteSpace(storedStamp) &&
        !string.IsNullOrWhiteSpace(sessionStamp) &&
        string.Equals(storedStamp, sessionStamp, StringComparison.Ordinal);
}
