using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Shared resolve-one precedence for host+plugin contributors: plugin-wins, with
/// a host-protection escape hatch. Both the job-handler and flow-action resolvers
/// route through here so the override semantics live in one place (R1).
/// </summary>
internal static class HostPluginResolution
{
    /// <summary>
    /// Resolves the contributor for <paramref name="key"/>. A host contributor
    /// marked <see cref="HostProtectedAttribute"/> keeps precedence; otherwise a
    /// plugin export of the same key wins, falling back to the host contributor.
    /// Matching is case-insensitive on <paramref name="keySelector"/>; within a
    /// source the last-registered contributor wins.
    /// </summary>
    public static T? ResolvePluginWins<T>(
        IEnumerable<T> hostContributors,
        IReadOnlyList<T> pluginContributors,
        Func<T, string> keySelector,
        string key)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(hostContributors);
        ArgumentNullException.ThrowIfNull(pluginContributors);
        ArgumentNullException.ThrowIfNull(keySelector);

        bool Matches(T contributor) =>
            string.Equals(keySelector(contributor), key, StringComparison.OrdinalIgnoreCase);

        var hostMatch = hostContributors.LastOrDefault(Matches);
        if (hostMatch is not null &&
            hostMatch.GetType().IsDefined(typeof(HostProtectedAttribute), inherit: false))
        {
            return hostMatch;
        }

        var pluginMatch = pluginContributors.LastOrDefault(Matches);
        return pluginMatch ?? hostMatch;
    }
}
