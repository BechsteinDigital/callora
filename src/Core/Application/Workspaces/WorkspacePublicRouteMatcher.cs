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
        var normalizedPath = PublicRouteMatching.NormalizePath(requestPath);

        WorkspaceSnapshot? best = null;
        var bestScore = int.MinValue;

        foreach (var workspace in candidates)
        {
            if (!workspace.IsActive || !workspace.TenantIsActive)
            {
                continue;
            }

            if (!PublicRouteMatching.HostMatches(workspace.PublicHost, normalizedHost) ||
                !PublicRouteMatching.PathMatches(workspace.PublicPathPrefix, normalizedPath))
            {
                continue;
            }

            var score = PublicRouteMatching.Score(workspace.PublicHost, workspace.PublicPathPrefix);
            if (score <= bestScore)
            {
                continue;
            }

            best = workspace;
            bestScore = score;
        }

        return best;
    }
}
