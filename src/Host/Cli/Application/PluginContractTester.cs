using System.Reflection;
using System.Text.Json;
using VoipHost.PluginContracts.Domain.Plugins;

namespace Callora.Host.Cli.Application;

internal sealed class PluginContractTester
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PluginContractTestResult> TestAsync(
        PluginContractTestRequest request,
        CancellationToken cancellationToken)
    {
        var issues = new List<PluginContractTestIssue>();

        var assemblyPath = Path.GetFullPath(request.AssemblyPath);
        if (!File.Exists(assemblyPath))
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.AssemblyNotFound,
                $"Plugin assembly was not found: '{assemblyPath}'.",
                "Build the plugin project and pass the generated DLL path via --assembly."));
            return PluginContractTestResult.Failure(issues);
        }

        var registryPath = ResolveRegistryPath(assemblyPath, request.RegistryPath);
        if (!File.Exists(registryPath))
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestNotFound,
                $"Manifest file registry.json was not found: '{registryPath}'.",
                "Create registry.json next to the plugin assembly or pass --registry <path>."));
            return PluginContractTestResult.Failure(issues);
        }

        var manifest = await ReadManifestAsync(registryPath, issues, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
            return PluginContractTestResult.Failure(issues);

        ValidateManifestRequiredFields(manifest, assemblyPath, issues);
        ValidateAssemblyContracts(assemblyPath, request.EntryTypeName, manifest.EntryTypeName, issues);

        return issues.Count == 0
            ? PluginContractTestResult.Success()
            : PluginContractTestResult.Failure(issues);
    }

    private static string ResolveRegistryPath(string assemblyPath, string? explicitRegistryPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitRegistryPath))
            return Path.GetFullPath(explicitRegistryPath);

        var assemblyDirectory = Path.GetDirectoryName(assemblyPath) ?? Directory.GetCurrentDirectory();
        return Path.Combine(assemblyDirectory, "registry.json");
    }

    private static async Task<PluginRegistryManifest?> ReadManifestAsync(
        string registryPath,
        ICollection<PluginContractTestIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(registryPath, cancellationToken).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<PluginRegistryManifest>(json, JsonOptions);
            if (manifest is not null)
                return manifest;

            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestParseError,
                "registry.json is empty.",
                "Provide valid JSON with all required registry fields."));
            return null;
        }
        catch (JsonException ex)
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestParseError,
                $"registry.json parse error: {ex.Message}",
                "Fix JSON syntax in registry.json and rerun the check."));
            return null;
        }
    }

    private static void ValidateManifestRequiredFields(
        PluginRegistryManifest manifest,
        string assemblyPath,
        ICollection<PluginContractTestIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(manifest.ContractVersion))
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestContractVersionMissing,
                "registry.json field 'contractVersion' is required.",
                "Set 'contractVersion' to 'v1'."));
        }
        else if (!string.Equals(manifest.ContractVersion.Trim(), "v1", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestContractVersionUnsupported,
                $"registry.json contractVersion '{manifest.ContractVersion}' is not supported.",
                "Use supported contractVersion 'v1'."));
        }

        if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestSchemaVersionMissing,
                "registry.json field 'schemaVersion' is required.",
                "Set 'schemaVersion' to '1.0'."));
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestNameMissing,
                "registry.json field 'name' is required.",
                "Set a human-readable plugin name in 'name'."));
        }

        if (string.IsNullOrWhiteSpace(manifest.PluginId))
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestPluginIdMissing,
                "registry.json field 'pluginId' is required.",
                "Set a stable identifier in 'pluginId' (e.g. 'acme-voice')."));
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestVersionMissing,
                "registry.json field 'version' is required.",
                "Set semantic plugin version in 'version' (e.g. '0.1.0')."));
        }

        if (string.IsNullOrWhiteSpace(manifest.AssemblyFileName))
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestAssemblyFileNameMissing,
                "registry.json field 'assemblyFileName' is required.",
                "Set the plugin assembly file name, for example 'Callora.Plugins.MyPlugin.dll'."));
        }
        else
        {
            var expectedFileName = Path.GetFileName(assemblyPath);
            if (!string.Equals(manifest.AssemblyFileName.Trim(), expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new PluginContractTestIssue(
                    PluginContractTestIssueCodes.ManifestAssemblyFileNameMismatch,
                    $"registry.json assemblyFileName '{manifest.AssemblyFileName}' does not match assembly '{expectedFileName}'.",
                    "Set assemblyFileName to the actual built DLL file name."));
            }
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryTypeName))
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.ManifestEntryTypeNameMissing,
                "registry.json field 'entryTypeName' is required.",
                "Set the full .NET type name of the plugin entrypoint class."));
        }
    }

    private static void ValidateAssemblyContracts(
        string assemblyPath,
        string? commandEntryTypeName,
        string? manifestEntryTypeName,
        ICollection<PluginContractTestIssue> issues)
    {
        var loadContext = new PluginInspectionLoadContext(assemblyPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            ValidateCompatibility(assembly, issues);
            ValidateLifecycle(assembly, commandEntryTypeName, manifestEntryTypeName, issues);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static void ValidateCompatibility(Assembly assembly, ICollection<PluginContractTestIssue> issues)
    {
        var hostContractsReference = assembly
            .GetReferencedAssemblies()
            .FirstOrDefault(static reference =>
                string.Equals(reference.Name, "VoipHost.PluginContracts", StringComparison.Ordinal));

        if (hostContractsReference is null)
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.CompatibilityContractsReferenceMissing,
                "Plugin does not reference VoipHost.PluginContracts.",
                "Add a reference to VoipHost.PluginContracts and rebuild the plugin."));
            return;
        }

        var pluginMajor = hostContractsReference.Version?.Major;
        var hostMajor = typeof(IHostManagedPlugin).Assembly.GetName().Version?.Major;
        if (pluginMajor.HasValue && hostMajor.HasValue && pluginMajor.Value != hostMajor.Value)
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.CompatibilityMajorMismatch,
                $"Plugin references VoipHost.PluginContracts major {pluginMajor.Value}, host expects {hostMajor.Value}.",
                "Align plugin contract package major version to the host contract major."));
        }
    }

    private static void ValidateLifecycle(
        Assembly assembly,
        string? commandEntryTypeName,
        string? manifestEntryTypeName,
        ICollection<PluginContractTestIssue> issues)
    {
        var entryTypeName = string.IsNullOrWhiteSpace(commandEntryTypeName)
            ? manifestEntryTypeName
            : commandEntryTypeName;

        Type? pluginType = null;
        if (!string.IsNullOrWhiteSpace(entryTypeName))
            pluginType = assembly.GetType(entryTypeName, throwOnError: false, ignoreCase: false);

        pluginType ??= assembly.GetTypes().FirstOrDefault(static type =>
            !type.IsAbstract
            && !type.IsInterface
            && type.GetInterfaces().Any(static iface =>
                string.Equals(
                    iface.FullName,
                    PluginLifecycleContractNames.HostManagedPluginInterface,
                    StringComparison.Ordinal)));

        if (pluginType is null)
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.LifecycleEntrypointNotFound,
                $"No plugin entrypoint type was found for '{entryTypeName ?? "(auto-detect)"}'.",
                "Set registry entryTypeName to a concrete type implementing IHostManagedPlugin."));
            return;
        }

        var implementsHostManagedPlugin = pluginType.GetInterfaces().Any(static iface =>
            string.Equals(
                iface.FullName,
                PluginLifecycleContractNames.HostManagedPluginInterface,
                StringComparison.Ordinal));
        if (!implementsHostManagedPlugin || pluginType.IsAbstract || pluginType.IsInterface)
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.LifecycleEntrypointInvalid,
                $"Entrypoint type '{pluginType.FullName}' is not a valid IHostManagedPlugin implementation.",
                "Implement IHostManagedPlugin on a concrete class and reference it as entryTypeName."));
            return;
        }

        var constructor = pluginType.GetConstructor(Type.EmptyTypes);
        if (constructor is null)
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.LifecycleEntrypointInstantiationFailed,
                $"Entrypoint type '{pluginType.FullName}' does not expose a public parameterless constructor.",
                "Add a public parameterless constructor or simplify plugin entrypoint instantiation."));
            return;
        }

        try
        {
            var instance = constructor.Invoke(null);
            var pluginId = pluginType.GetProperty("PluginId", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(instance) as string;
            var displayName = pluginType.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(instance) as string;

            if (string.IsNullOrWhiteSpace(pluginId))
            {
                issues.Add(new PluginContractTestIssue(
                    PluginContractTestIssueCodes.LifecyclePluginIdMissing,
                    "Entrypoint property PluginId is empty.",
                    "Return a stable non-empty PluginId value from the plugin class."));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                issues.Add(new PluginContractTestIssue(
                    PluginContractTestIssueCodes.LifecycleDisplayNameMissing,
                    "Entrypoint property DisplayName is empty.",
                    "Return a human-readable non-empty DisplayName value from the plugin class."));
            }
        }
        catch (Exception ex)
        {
            issues.Add(new PluginContractTestIssue(
                PluginContractTestIssueCodes.LifecycleEntrypointInstantiationFailed,
                $"Failed to instantiate plugin entrypoint '{pluginType.FullName}': {ex.GetBaseException().Message}",
                "Ensure entrypoint constructor has no runtime dependencies and does not throw."));
        }
    }
}
