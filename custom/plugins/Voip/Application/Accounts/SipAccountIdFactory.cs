namespace Callora.Plugins.Voip.Application.Accounts;

/// <summary>
/// Builds stable, URL-safe SIP account identifiers from username and domain.
/// </summary>
public static class SipAccountIdFactory
{
    public static string Create(string username, string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        var normalizedUsername = username.Trim().ToLowerInvariant();
        var normalizedDomain = domain.Trim().ToLowerInvariant();

        var joined = $"{normalizedUsername}@{normalizedDomain}";
        var safeChars = joined
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or '@' ? ch : '-')
            .ToArray();
        return new string(safeChars);
    }
}
