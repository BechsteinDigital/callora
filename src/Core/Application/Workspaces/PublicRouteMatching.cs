namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Shared host/path matching for public-route resolution. Used by both the (legacy)
/// workspace matcher and the surface-based resolution so the two never drift.
/// </summary>
internal static class PublicRouteMatching
{
    public static bool HostMatches(string? configuredHost, string requestHost)
    {
        if (string.IsNullOrWhiteSpace(configuredHost))
        {
            return true;
        }

        return string.Equals(configuredHost.Trim(), requestHost, StringComparison.OrdinalIgnoreCase);
    }

    public static bool PathMatches(string configuredPrefix, string requestPath)
    {
        var prefix = NormalizePath(configuredPrefix);
        if (prefix == "/")
        {
            return true;
        }

        if (string.Equals(requestPath, prefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return requestPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>More specific matches win: a fixed host beats a wildcard, a longer prefix beats a shorter.</summary>
    public static int Score(string? host, string prefix) =>
        (string.IsNullOrWhiteSpace(host) ? 0 : 10000) + prefix.Length;

    public static string NormalizePath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "/";
        }

        var path = input.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        while (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
        {
            path = path[..^1];
        }

        return path;
    }
}
