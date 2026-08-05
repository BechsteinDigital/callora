using Callora.Core.Application.Persistence;
using Semver;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Answers what an installation actually offers in the way of plugin contracts (#125 block D).
/// <para>
/// It joins two things the host already knows but never showed together: which contract assemblies
/// are shared across load contexts, and which installed plugins declared a dependency on them. The
/// join is the point. A version on its own says nothing about consequences; a version plus the
/// plugins bound to it says what an update would break.
/// </para>
/// </summary>
public sealed class ContractCatalogService(
    SharedContractAssemblyRegistry sharedContracts,
    IPluginInstallationRepository installations,
    IPluginPackageRegistryReader? registryReader = null)
{
    /// <summary>Lists every shared contract with its dependents, ordered by assembly name.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<ContractCatalogEntry>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var dependencies = await ReadDependenciesAsync(cancellationToken).ConfigureAwait(false);

        return sharedContracts.ListRegistrations()
            .Select(registration => new ContractCatalogEntry(
                registration.AssemblyName,
                registration.Version,
                registration.DeclaringPluginId,
                registration.IsHostProvided,
                // Stated rather than implied: pinning is why a contract change costs a restart.
                RequiresRestartToChange: true,
                DependentsOf(registration, dependencies)))
            .ToArray();
    }

    private static IReadOnlyList<ContractDependent> DependentsOf(
        SharedContractRegistration registration,
        IReadOnlyList<(string PluginId, string ContractId, string Range)> dependencies)
    {
        var pinned = ParseVersion(registration.Version);

        return dependencies
            .Where(entry => string.Equals(
                entry.ContractId, registration.AssemblyName, StringComparison.OrdinalIgnoreCase))
            .Select(entry => new ContractDependent(
                entry.PluginId, entry.Range, IsSatisfied(entry.Range, pinned)))
            .OrderBy(static dependent => dependent.PluginId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsSatisfied(string rangeText, SemVersion? pinned)
    {
        // An unreadable range or version is reported as unsatisfied rather than assumed fine: the
        // catalog exists to warn, and a silent "probably ok" would defeat that.
        if (pinned is null || !SemVersionRange.TryParseNpm(rangeText, out var range) || range is null)
        {
            return false;
        }

        return range.Contains(pinned);
    }

    private static SemVersion? ParseVersion(string version)
    {
        // Assembly versions are four-part; SemVer takes the first three.
        var parts = version.Split('.');
        return parts.Length >= 3 &&
               int.TryParse(parts[0], out var major) &&
               int.TryParse(parts[1], out var minor) &&
               int.TryParse(parts[2], out var patch)
            ? new SemVersion(major, minor, patch)
            : null;
    }

    private async Task<IReadOnlyList<(string PluginId, string ContractId, string Range)>> ReadDependenciesAsync(
        CancellationToken cancellationToken)
    {
        if (registryReader is null)
        {
            return [];
        }

        var declared = new List<(string, string, string)>();
        foreach (var installation in await installations.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(installation.AssemblyPath))
            {
                continue;
            }

            var result = await registryReader
                .ReadForAssemblyAsync(installation.AssemblyPath, cancellationToken)
                .ConfigureAwait(false);
            var dependencies = result.Registry?.Dependencies;
            if (dependencies is null)
            {
                continue;
            }

            foreach (var (contractId, range) in dependencies)
            {
                if (!string.IsNullOrWhiteSpace(contractId))
                {
                    declared.Add((installation.PluginId, contractId, range ?? string.Empty));
                }
            }
        }

        return declared;
    }
}
