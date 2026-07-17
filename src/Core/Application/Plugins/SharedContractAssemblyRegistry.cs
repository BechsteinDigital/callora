using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.Loader;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Shared identity for plugin-provided contract assemblies (PLAT-256).
/// Assemblies declared under "contracts" in a plugin's registry.json are
/// loaded exactly once into the default load context, so every plugin
/// resolving the same contract sees the same .NET type identities — the
/// host itself never has to reference third-party contracts.
/// Shared contracts are pinned for the host's lifetime: replacing one
/// requires a host restart, everything else stays hot-swappable.
/// </summary>
public sealed class SharedContractAssemblyRegistry(ILogger? logger = null)
{
    private readonly object _syncLock = new();
    private readonly Dictionary<string, Assembly> _assembliesByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loads the declared contract assemblies of one plugin into the shared
    /// context. First registration of a name wins; a later registration with
    /// a different major version fails the plugin operation.
    /// </summary>
    public void RegisterContracts(string pluginDirectory, IReadOnlyCollection<string> contractFileNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        ArgumentNullException.ThrowIfNull(contractFileNames);

        var rootPath = Path.GetFullPath(pluginDirectory);
        foreach (var fileName in contractFileNames)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(rootPath, fileName));
            if (!fullPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Contract declaration '{fileName}' escapes the plugin directory.");
            }

            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Declared contract assembly '{fileName}' is missing from the plugin package.");
            }

            RegisterContractAssembly(fullPath);
        }
    }

    /// <summary>
    /// Resolves a shared contract assembly by name; null when unknown or
    /// incompatible (caller falls back to plugin-local resolution).
    /// </summary>
    public Assembly? TryResolve(AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);
        if (assemblyName.Name is not { Length: > 0 } name)
        {
            return null;
        }

        lock (_syncLock)
        {
            if (!_assembliesByName.TryGetValue(name, out var assembly))
            {
                return null;
            }

            var loadedVersion = assembly.GetName().Version;
            if (assemblyName.Version is not null &&
                loadedVersion is not null &&
                assemblyName.Version.Major != loadedVersion.Major)
            {
                return null;
            }

            return assembly;
        }
    }

    private void RegisterContractAssembly(string fullPath)
    {
        var assemblyName = AssemblyName.GetAssemblyName(fullPath);
        if (assemblyName.Name is not { Length: > 0 } name)
        {
            throw new InvalidOperationException($"Contract assembly '{fullPath}' has no assembly name.");
        }

        // Callora.*-Verträge teilen ihre Identität bereits über den
        // Default-Kontext des Hosts — keine Doppelregistrierung.
        if (name.Equals("Callora", StringComparison.Ordinal) ||
            name.StartsWith("Callora.", StringComparison.Ordinal))
        {
            logger?.LogDebug("Contract declaration '{AssemblyName}' is host-shared already; skipping.", name);
            return;
        }

        lock (_syncLock)
        {
            if (_assembliesByName.TryGetValue(name, out var existing))
            {
                var existingVersion = existing.GetName().Version;
                if (existingVersion is not null &&
                    assemblyName.Version is not null &&
                    existingVersion.Major != assemblyName.Version.Major)
                {
                    throw new InvalidOperationException(
                        $"Contract assembly '{name}' is already shared as version {existingVersion}; " +
                        $"version {assemblyName.Version} has an incompatible major version.");
                }

                if (assemblyName.Version is not null && assemblyName.Version != existingVersion)
                {
                    logger?.LogInformation(
                        "Contract assembly {AssemblyName} {DeclaredVersion} reuses already-shared version {LoadedVersion}.",
                        name,
                        assemblyName.Version,
                        existingVersion);
                }

                return;
            }

            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
            _assembliesByName[name] = assembly;
            logger?.LogInformation(
                "Shared contract assembly {AssemblyName} {Version} loaded from {Path}.",
                name,
                assemblyName.Version,
                fullPath);
        }
    }
}
