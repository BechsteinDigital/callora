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
    private readonly Dictionary<string, SharedContractRegistration> _registrationsByName =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loads the declared contract assemblies of one plugin into the shared
    /// context. First registration of a name wins; a later registration with
    /// a different major version fails the plugin operation.
    /// </summary>
    /// <param name="pluginDirectory">Directory the plugin was installed into.</param>
    /// <param name="contractFileNames">File names declared under "contracts" in its manifest.</param>
    /// <param name="declaringPluginId">Plugin that declared them, for the catalog.</param>
    public void RegisterContracts(
        string pluginDirectory,
        IReadOnlyCollection<string> contractFileNames,
        string? declaringPluginId = null)
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

            RegisterContractAssembly(fullPath, declaringPluginId);
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

    /// <summary>
    /// Everything currently shared, host-provided and plugin-provided alike, ordered by name.
    /// </summary>
    public IReadOnlyList<SharedContractRegistration> ListRegistrations()
    {
        lock (_syncLock)
        {
            return _registrationsByName.Values
                .OrderBy(static registration => registration.AssemblyName, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private void RegisterContractAssembly(string fullPath, string? declaringPluginId)
    {
        var assemblyName = AssemblyName.GetAssemblyName(fullPath);
        if (assemblyName.Name is not { Length: > 0 } name)
        {
            throw new InvalidOperationException($"Contract assembly '{fullPath}' has no assembly name.");
        }

        // The Callora. prefix means "the host provides this": the plugin load context delegates
        // those names to the default context instead of loading them locally. That only works when
        // the host application actually references the assembly. A plugin-provided contract
        // carrying the prefix would be skipped here AND absent from the default context, so it
        // would fail to load the moment the plugin touched one of its types — a defect that
        // previously surfaced as a debug line and a crash at plugin start.
        if (name.Equals("Callora", StringComparison.Ordinal) ||
            name.StartsWith("Callora.", StringComparison.Ordinal))
        {
            if (TryResolveFromDefaultContext(name) is not { } hostProvided)
            {
                throw new InvalidOperationException(
                    $"Declared contract '{name}' uses the reserved 'Callora.' prefix but the host does " +
                    "not provide it. Name a plugin-provided contract outside that prefix so it can be " +
                    "shared across load contexts.");
            }

            lock (_syncLock)
            {
                _registrationsByName.TryAdd(
                    name,
                    new SharedContractRegistration(
                        name, VersionOf(hostProvided), declaringPluginId, IsHostProvided: true));
            }

            logger?.LogDebug("Contract declaration '{AssemblyName}' is host-provided; recorded only.", name);
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

                // A later declarer of an already-shared contract still belongs in the catalog's
                // dependents, but the first registration keeps ownership of the identity.
                return;
            }

            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
            _assembliesByName[name] = assembly;
            _registrationsByName[name] = new SharedContractRegistration(
                name, VersionOf(assembly), declaringPluginId, IsHostProvided: false);
            logger?.LogInformation(
                "Shared contract assembly {AssemblyName} {Version} loaded from {Path}.",
                name,
                assemblyName.Version,
                fullPath);
        }
    }

    private static Assembly? TryResolveFromDefaultContext(string name)
    {
        try
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name));
        }
        catch (Exception exception)
            when (exception is FileNotFoundException or FileLoadException or BadImageFormatException
                      or ArgumentException)
        {
            return null;
        }
    }

    private static string VersionOf(Assembly assembly) =>
        assembly.GetName().Version?.ToString() ?? "0.0.0.0";
}
