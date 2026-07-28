using Callora.Core.Application.Plugins;
using Callora.Core.Extensibility;
using Microsoft.Extensions.Logging;
using Semver;
using System.Reflection;
using System.Runtime.Loader;

namespace Callora.Core.Infrastructure.Plugins;

/// <summary>
/// Resolves the version the host provides for a plugin dependency from the loaded
/// assemblies the plugin actually binds against (ABI compatibility): first any
/// plugin-provided shared contract in the <see cref="SharedContractAssemblyRegistry"/>,
/// then the framework/shared assemblies loaded in the default load context
/// (e.g. <c>Callora.Core</c>). Unknown dependencies resolve to <c>null</c>.
/// </summary>
[CalloraInternal("Dependency-version resolution over loaded assemblies — not a plugin contract")]
public sealed class LoadedContractVersionProvider(
    SharedContractAssemblyRegistry? sharedContracts = null,
    ILogger<LoadedContractVersionProvider>? logger = null)
    : IProvidedContractVersionProvider
{
    /// <inheritdoc />
    public SemVersion? Resolve(string contractId)
    {
        if (string.IsNullOrWhiteSpace(contractId))
        {
            return null;
        }

        var assembly = ResolveAssembly(contractId);
        if (assembly is null)
        {
            return null;
        }

        return ReadVersion(assembly);
    }

    private Assembly? ResolveAssembly(string contractId)
    {
        // Plugin-provided shared contracts win (they carry the identity the plugin binds to).
        if (sharedContracts is not null)
        {
            try
            {
                var shared = sharedContracts.TryResolve(new AssemblyName(contractId));
                if (shared is not null)
                {
                    return shared;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FileLoadException)
            {
                logger?.LogDebug(ex, "Contract id '{ContractId}' is not a valid assembly name for shared resolution.", contractId);
            }
        }

        // Framework/shared assemblies loaded in the default context (e.g. Callora.Core).
        foreach (var loaded in AssemblyLoadContext.Default.Assemblies)
        {
            var name = loaded.GetName().Name;
            if (string.Equals(name, contractId, StringComparison.OrdinalIgnoreCase))
            {
                return loaded;
            }
        }

        return null;
    }

    private SemVersion? ReadVersion(Assembly assembly)
    {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational) &&
            TryParseInformational(informational, out var fromInformational))
        {
            return fromInformational;
        }

        // Fallback: the AssemblyVersion (System.Version → SemVer major.minor.patch).
        var version = assembly.GetName().Version;
        if (version is null)
        {
            logger?.LogDebug("Assembly {Assembly} exposes no readable version.", assembly.FullName);
            return null;
        }

        return new SemVersion(
            Math.Max(version.Major, 0),
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0));
    }

    private static bool TryParseInformational(string informational, out SemVersion? version)
    {
        // Some tools append build metadata after '+'; SemVer parses it, but trimming it
        // keeps the compared version focused on the release/prerelease identity.
        var plus = informational.IndexOf('+');
        var core = plus >= 0 ? informational[..plus] : informational;
        return SemVersion.TryParse(core, SemVersionStyles.Any, out version);
    }
}
