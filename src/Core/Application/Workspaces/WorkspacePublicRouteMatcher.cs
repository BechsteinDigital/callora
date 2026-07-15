namespace Callora.Core.Application.Workspaces;

public static class WorkspacePublicRouteMatcher
{
    public static WorkspaceSnapshot? ResolveBest(
        IEnumerable<WorkspaceSnapshot> candidates,
        string requestHost,
        string requestPath)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var normalizedHost = (requestHost ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedPath = NormalizePath(requestPath);

        WorkspaceSnapshot? best = null;
        var bestScore = int.MinValue;

        foreach (var workspace in candidates)
        {
            if (!workspace.IsActive || !workspace.TenantIsActive)
            {
                continue;
            }

            if (!HostMatches(workspace.PublicHost, normalizedHost))
            {
                continue;
            }

            if (!PathMatches(workspace.PublicPathPrefix, normalizedPath))
            {
                continue;
            }

            var score = ComputeScore(workspace);
            if (score <= bestScore)
            {
                continue;
            }

            best = workspace;
            bestScore = score;
        }

        return best;
    }

    private static bool HostMatches(string? configuredHost, string requestHost)
    {
        if (string.IsNullOrWhiteSpace(configuredHost))
        {
            return true;
        }

        return string.Equals(configuredHost.Trim(), requestHost, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathMatches(string configuredPrefix, string requestPath)
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

    private static int ComputeScore(WorkspaceSnapshot workspace)
    {
        var hostScore = string.IsNullOrWhiteSpace(workspace.PublicHost) ? 0 : 10000;
        return hostScore + workspace.PublicPathPrefix.Length;
    }

    private static string NormalizePath(string? input)
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
