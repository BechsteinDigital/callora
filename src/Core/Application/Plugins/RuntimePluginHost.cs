using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Host.PluginContracts.Domain.Plugins;
using Callora.Core.Application.Options;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Runtime plugin host backed by collectible assembly load contexts.
/// </summary>
public sealed class RuntimePluginHost : ICalloraPluginRuntime, IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly CalloraHostingOptions _options;
    private readonly ILogger<RuntimePluginHost> _logger;
    private readonly ConcurrentDictionary<string, InstalledPluginRecord> _installed = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ActivePluginHandle> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Type, ImmutableArray<PluginExportRegistration>> _exports = new();
    private readonly SemaphoreSlim _mutationLock = new(1, 1);
    private readonly SharedContractAssemblyRegistry _sharedContracts;
    private long _exportSequence;

    /// <summary>
    /// Creates a runtime plugin host.
    /// </summary>
    public RuntimePluginHost(
        IServiceProvider services,
        CalloraHostingOptions options,
        ILogger<RuntimePluginHost> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
        _sharedContracts = new SharedContractAssemblyRegistry(logger);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<RuntimePluginDescriptor> LoadedPlugins =>
        _installed.Values
            .Select(ToDescriptor)
            .OrderBy(static plugin => plugin.PluginId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <inheritdoc />
    public bool TryGetExport(Type contractType, out object? service)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        if (_exports.TryGetValue(contractType, out var registrations) && registrations.Length > 0)
        {
            // Highest sequence wins to keep deterministic "latest active export" behavior.
            service = registrations
                .OrderByDescending(static registration => registration.Sequence)
                .First()
                .Service;
            return true;
        }

        service = null;
        return false;
    }

    // Cross-plugin service resolution for the curated provider (REV2 §9.3):
    // a contract the host does not register itself is served from a plugin
    // export. Only single-provider shared services (e.g. the channel registry)
    // are resolvable this way — multi-provider exports (controllers, flow
    // handlers, event providers) are host-collected and must not be picked
    // arbitrarily through a consuming plugin's service surface. Withdrawn
    // automatically on the providing plugin's deactivation.
    private object? ResolveExport(Type contractType) =>
        _exports.TryGetValue(contractType, out var registrations) && registrations.Length == 1
            ? registrations[0].Service
            : null;

    /// <inheritdoc />
    public IReadOnlyList<object> GetExports(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        if (!_exports.TryGetValue(contractType, out var registrations) || registrations.Length == 0)
            return Array.Empty<object>();

        return registrations
            .OrderByDescending(static registration => registration.Sequence)
            .Select(static registration => registration.Service)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        if (!_exports.TryGetValue(contractType, out var registrations) || registrations.Length == 0)
            return Array.Empty<CalloraPluginExport>();

        return registrations
            .OrderByDescending(static registration => registration.Sequence)
            .Select(static registration => new CalloraPluginExport(registration.PluginId, registration.Service))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<RuntimePluginInstallResult> InstallAsync(
        string assemblyPath,
        string? entryTypeName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return new RuntimePluginInstallResult(
                RuntimePluginInstallStatus.InvalidPath,
                Plugin: null,
                Message: "Plugin assembly path is empty.");
        }

        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            return new RuntimePluginInstallResult(
                RuntimePluginInstallStatus.InvalidPath,
                Plugin: null,
                Message: $"Plugin assembly '{fullPath}' does not exist.");
        }

        await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var inspection = InspectPluginAssembly(fullPath, entryTypeName);
            if (inspection.Result is not null)
                return inspection.Result;

            var record = inspection.Record!;
            if (_installed.TryGetValue(record.PluginId, out var existing))
            {
                return new RuntimePluginInstallResult(
                    RuntimePluginInstallStatus.AlreadyInstalled,
                    ToDescriptor(existing),
                    $"Plugin '{record.PluginId}' is already installed.");
            }

            _installed[record.PluginId] = record;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Installed plugin {PluginId} from {AssemblyPath}.", record.PluginId, fullPath);
            }
            return new RuntimePluginInstallResult(RuntimePluginInstallStatus.Installed, ToDescriptor(record));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin install failed for assembly {AssemblyPath}.", fullPath);
            return new RuntimePluginInstallResult(RuntimePluginInstallStatus.Failed, Plugin: null, ex.Message);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RuntimePluginActivateResult> ActivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return new RuntimePluginActivateResult(
                RuntimePluginActivateStatus.NotInstalled,
                pluginId,
                "Plugin id is empty.");
        }

        await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_installed.TryGetValue(pluginId, out var record))
            {
                return new RuntimePluginActivateResult(
                    RuntimePluginActivateStatus.NotInstalled,
                    pluginId,
                    $"Plugin '{pluginId}' is not installed.");
            }

            if (_active.ContainsKey(pluginId))
            {
                return new RuntimePluginActivateResult(
                    RuntimePluginActivateStatus.AlreadyActive,
                    pluginId);
            }

            var activation = await ActivateInternalAsync(record, cancellationToken).ConfigureAwait(false);
            if (!activation.IsSuccess)
            {
                // Fehlgeschlagene Aktivierung ist sichtbar Faulted, nicht
                // stillschweigend "installiert" (PLAT-255).
                record.State = RuntimePluginState.Faulted;
                _installed[record.PluginId] = record;
                return activation;
            }

            record.State = RuntimePluginState.Active;
            _installed[record.PluginId] = record;
            return activation;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RuntimePluginDeactivateResult> DeactivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return new RuntimePluginDeactivateResult(
                RuntimePluginDeactivateStatus.NotInstalled,
                pluginId,
                "Plugin id is empty.");
        }

        await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_installed.TryGetValue(pluginId, out var record))
            {
                return new RuntimePluginDeactivateResult(
                    RuntimePluginDeactivateStatus.NotInstalled,
                    pluginId,
                    $"Plugin '{pluginId}' is not installed.");
            }

            if (!_active.ContainsKey(pluginId))
            {
                return new RuntimePluginDeactivateResult(
                    RuntimePluginDeactivateStatus.AlreadyInactive,
                    pluginId);
            }

            var deactivation = await DeactivateInternalAsync(pluginId, cancellationToken).ConfigureAwait(false);
            if (!deactivation.IsSuccess)
            {
                // Teardown-Fehler pinnen ggf. Ressourcen bis zum Neustart —
                // als UnloadFailed ausgewiesen statt verschluckt (PLAT-255).
                record.State = RuntimePluginState.UnloadFailed;
                _installed[record.PluginId] = record;
                return deactivation;
            }

            record.State = RuntimePluginState.Inactive;
            _installed[record.PluginId] = record;
            return deactivation;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RuntimePluginUninstallResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return new RuntimePluginUninstallResult(
                RuntimePluginUninstallStatus.NotInstalled,
                pluginId,
                "Plugin id is empty.");
        }

        await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_installed.TryGetValue(pluginId, out _))
            {
                return new RuntimePluginUninstallResult(
                    RuntimePluginUninstallStatus.NotInstalled,
                    pluginId,
                    $"Plugin '{pluginId}' is not installed.");
            }

            if (_active.ContainsKey(pluginId))
            {
                var deactivate = await DeactivateInternalAsync(pluginId, cancellationToken).ConfigureAwait(false);
                if (!deactivate.IsSuccess)
                {
                    return new RuntimePluginUninstallResult(
                        RuntimePluginUninstallStatus.Failed,
                        pluginId,
                        deactivate.Message);
                }
            }

            _installed.TryRemove(pluginId, out _);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Uninstalled plugin {PluginId}.", pluginId);
            }
            return new RuntimePluginUninstallResult(RuntimePluginUninstallStatus.Uninstalled, pluginId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin uninstall failed for {PluginId}.", pluginId);
            return new RuntimePluginUninstallResult(RuntimePluginUninstallStatus.Failed, pluginId, ex.Message);
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _mutationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var pluginId in _active.Keys.ToArray())
            {
                await DeactivateInternalAsync(pluginId, CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _mutationLock.Release();
            _mutationLock.Dispose();
        }
    }

    private async Task<RuntimePluginActivateResult> ActivateInternalAsync(
        InstalledPluginRecord record,
        CancellationToken cancellationToken)
    {
        PluginAssemblyLoadContext? loadContext = null;
        IHostManagedPlugin? plugin = null;

        try
        {
            RegisterDeclaredContracts(record.AssemblyPath);
            loadContext = new PluginAssemblyLoadContext(record.AssemblyPath, _sharedContracts);
            var assembly = loadContext.LoadFromAssemblyPath(record.AssemblyPath);
            var pluginType = ResolvePluginType(assembly, record.EntryTypeName);
            if (pluginType is null)
            {
                loadContext.Unload();
                return new RuntimePluginActivateResult(
                    RuntimePluginActivateStatus.Failed,
                    record.PluginId,
                    "Plugin entrypoint type not found.");
            }

            var created = CreatePluginInstance(pluginType);
            if (created is null)
            {
                loadContext.Unload();
                return new RuntimePluginActivateResult(
                    RuntimePluginActivateStatus.Failed,
                    record.PluginId,
                    $"Could not create plugin entrypoint '{pluginType.FullName}'.");
            }

            plugin = created;
            if (!string.Equals(plugin.PluginId, record.PluginId, StringComparison.OrdinalIgnoreCase))
            {
                loadContext.Unload();
                return new RuntimePluginActivateResult(
                    RuntimePluginActivateStatus.Failed,
                    record.PluginId,
                    $"Plugin id mismatch. Expected '{record.PluginId}', but plugin returned '{plugin.PluginId}'.");
            }

            var pluginContext = new PluginContext(_services, record.PluginId, RegisterExport, ResolveExport);
            await plugin.StartAsync(pluginContext, cancellationToken).ConfigureAwait(false);

            // Shopware-artige Controller-Discovery: IApiController-Typen der
            // Plugin-Assembly werden automatisch instanziiert (Ctor-DI über
            // die kuratierte Oberfläche) und exportiert (PLAT-257).
            RegisterApiControllers(record.PluginId, assembly, pluginContext.Services);

            var handle = new ActivePluginHandle(record.PluginId, plugin, loadContext);
            if (!_active.TryAdd(record.PluginId, handle))
            {
                await SafeStopAsync(plugin, cancellationToken).ConfigureAwait(false);
                RemoveExportsByPlugin(record.PluginId);
                loadContext.Unload();
                return new RuntimePluginActivateResult(
                    RuntimePluginActivateStatus.AlreadyActive,
                    record.PluginId);
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Activated plugin {PluginId}.", record.PluginId);
            }
            return new RuntimePluginActivateResult(RuntimePluginActivateStatus.Activated, record.PluginId);
        }
        catch (Exception ex)
        {
            RemoveExportsByPlugin(record.PluginId);

            if (plugin is not null)
                await SafeStopAsync(plugin, cancellationToken).ConfigureAwait(false);
            loadContext?.Unload();

            _logger.LogError(ex, "Plugin activation failed for {PluginId}.", record.PluginId);
            return new RuntimePluginActivateResult(RuntimePluginActivateStatus.Failed, record.PluginId, ex.Message);
        }
    }

    private async Task<RuntimePluginDeactivateResult> DeactivateInternalAsync(
        string pluginId,
        CancellationToken cancellationToken)
    {
        if (!_active.TryRemove(pluginId, out var handle))
        {
            return new RuntimePluginDeactivateResult(RuntimePluginDeactivateStatus.AlreadyInactive, pluginId);
        }

        WeakReference loadContextReference;
        try
        {
            RemoveExportsByPlugin(pluginId);
            await SafeStopAsync(handle.Plugin, cancellationToken).ConfigureAwait(false);
            loadContextReference = UnloadAndTrack(handle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin deactivation failed for {PluginId}.", pluginId);
            return new RuntimePluginDeactivateResult(RuntimePluginDeactivateStatus.Failed, pluginId, ex.Message);
        }

        // Drop the last strong reference before verifying collection; otherwise
        // this frame would pin the context and always report a false failure.
        handle = null!;

        if (!AssemblyLoadContextUnload.WaitForCollection(loadContextReference))
        {
            _logger.LogError(
                "Plugin {PluginId} was stopped but its assembly load context is still pinned after unload.",
                pluginId);
            return new RuntimePluginDeactivateResult(
                RuntimePluginDeactivateStatus.Failed,
                pluginId,
                "Plugin was stopped, but its assembly load context is still pinned after unload; a host restart is required to fully release it.");
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Deactivated plugin {PluginId}.", pluginId);
        }
        return new RuntimePluginDeactivateResult(RuntimePluginDeactivateStatus.Deactivated, pluginId);
    }

    // Non-inlined so no caller-frame local keeps the load context alive while we
    // verify it was collected.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference UnloadAndTrack(ActivePluginHandle handle)
    {
        handle.LoadContext.Unload();
        return new WeakReference(handle.LoadContext);
    }

    private (InstalledPluginRecord? Record, RuntimePluginInstallResult? Result) InspectPluginAssembly(
        string fullPath,
        string? entryTypeName)
    {
        PluginAssemblyLoadContext? loadContext = null;
        IHostManagedPlugin? plugin = null;

        try
        {
            RegisterDeclaredContracts(fullPath);
            loadContext = new PluginAssemblyLoadContext(fullPath, _sharedContracts);
            var assembly = loadContext.LoadFromAssemblyPath(fullPath);
            var pluginType = ResolvePluginType(assembly, entryTypeName);
            if (pluginType is null)
            {
                loadContext.Unload();
                return (null, new RuntimePluginInstallResult(
                    RuntimePluginInstallStatus.EntryPointNotFound,
                    Plugin: null,
                    Message: "No host plugin entrypoint found."));
            }

            if (!IsSupportedPluginType(pluginType) ||
                pluginType.IsAbstract ||
                pluginType.IsInterface)
            {
                loadContext.Unload();
                return (null, new RuntimePluginInstallResult(
                    RuntimePluginInstallStatus.EntryPointInvalid,
                    Plugin: null,
                    Message: $"Type '{pluginType.FullName}' is not a valid host plugin entrypoint."));
            }

            var compatibility = ValidateHostCompatibility(assembly);
            if (compatibility is not null)
            {
                loadContext.Unload();
                return (null, new RuntimePluginInstallResult(
                    RuntimePluginInstallStatus.Failed,
                    Plugin: null,
                    Message: compatibility));
            }

            var created = CreatePluginInstance(pluginType);
            if (created is null)
            {
                loadContext.Unload();
                return (null, new RuntimePluginInstallResult(
                    RuntimePluginInstallStatus.EntryPointInvalid,
                    Plugin: null,
                    Message: $"Could not create plugin entrypoint '{pluginType.FullName}'."));
            }

            plugin = created;
            var record = new InstalledPluginRecord(
                plugin.PluginId,
                plugin.DisplayName,
                fullPath,
                pluginType.FullName,
                RuntimePluginState.Installed);

            return (record, null);
        }
        catch (Exception ex)
        {
            return (null, new RuntimePluginInstallResult(RuntimePluginInstallStatus.Failed, Plugin: null, ex.Message));
        }
        finally
        {
            switch (plugin)
            {
                case IAsyncDisposable asyncDisposable:
                    asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }

            loadContext?.Unload();
        }
    }

    private static string? ValidateHostCompatibility(Assembly pluginAssembly)
    {
        var hostContractsRef = pluginAssembly
            .GetReferencedAssemblies()
            .FirstOrDefault(static reference => string.Equals(
                reference.Name,
                typeof(IHostManagedPlugin).Assembly.GetName().Name,
                StringComparison.Ordinal));

        var pluginVersion = hostContractsRef?.Version;
        if (pluginVersion is null)
            return null;

        var hostVersion = typeof(IHostManagedPlugin).Assembly.GetName().Version;
        if (hostVersion is null)
            return null;

        if (pluginVersion.Major != hostVersion.Major)
        {
            return $"Plugin targets host contracts {pluginVersion}, but host uses {hostVersion}.";
        }

        return null;
    }

    private void RegisterDeclaredContracts(string pluginAssemblyPath)
    {
        var declaredContracts = PluginContractManifestReader.ReadDeclaredContracts(pluginAssemblyPath);
        if (declaredContracts.Count == 0)
        {
            return;
        }

        var pluginDirectory = Path.GetDirectoryName(Path.GetFullPath(pluginAssemblyPath))
            ?? throw new InvalidOperationException($"Plugin path '{pluginAssemblyPath}' has no directory.");
        _sharedContracts.RegisterContracts(pluginDirectory, declaredContracts);
    }

    private static Type? ResolvePluginType(Assembly assembly, string? entryTypeName)
    {
        if (!string.IsNullOrWhiteSpace(entryTypeName))
            return assembly.GetType(entryTypeName, throwOnError: false, ignoreCase: false);

        return assembly
            .GetTypes()
            .FirstOrDefault(static type => IsSupportedPluginType(type) &&
                                           !type.IsAbstract &&
                                           !type.IsInterface);
    }

    private static bool IsSupportedPluginType(Type type) =>
        typeof(IHostManagedPlugin).IsAssignableFrom(type);

    private static IHostManagedPlugin? CreatePluginInstance(Type pluginType) =>
        Activator.CreateInstance(pluginType) as IHostManagedPlugin;

    private void RegisterApiControllers(string pluginId, Assembly pluginAssembly, IServiceProvider pluginServices)
    {
        Type[] assemblyTypes;
        try
        {
            assemblyTypes = pluginAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            assemblyTypes = exception.Types.Where(static type => type is not null).ToArray()!;
        }

        var manuallyExported = GetExports(typeof(Callora.Host.PluginContracts.Application.Http.IApiController))
            .Select(static export => export.GetType())
            .ToHashSet();

        foreach (var controllerType in assemblyTypes)
        {
            if (controllerType.IsAbstract ||
                controllerType.IsInterface ||
                !typeof(Callora.Host.PluginContracts.Application.Http.IApiController).IsAssignableFrom(controllerType))
            {
                continue;
            }

            // Vom Plugin in StartAsync selbst exportierte Controller (eigene
            // Ctor-Abhängigkeiten) werden nicht doppelt instanziiert.
            if (manuallyExported.Contains(controllerType))
            {
                continue;
            }

            // Ctor-Fehler (nicht kuratierter Service) lassen die Aktivierung
            // bewusst laut scheitern statt Routen still zu verlieren.
            var controller = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(
                pluginServices,
                controllerType);
            RegisterExport(pluginId, typeof(Callora.Host.PluginContracts.Application.Http.IApiController), controller);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Registered API controller {ControllerType} for plugin {PluginId}.",
                    controllerType.FullName,
                    pluginId);
            }
        }
    }

    private void RegisterExport(string pluginId, Type contractType, object service)
    {
        ArgumentNullException.ThrowIfNull(pluginId);
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentNullException.ThrowIfNull(service);

        if (!contractType.IsInstanceOfType(service))
        {
            throw new InvalidOperationException(
                $"Export instance type '{service.GetType().FullName}' does not implement '{contractType.FullName}'.");
        }

        var registration = new PluginExportRegistration(
            pluginId,
            contractType,
            service,
            Interlocked.Increment(ref _exportSequence));

        _exports.AddOrUpdate(
            contractType,
            _ => ImmutableArray.Create(registration),
            (_, current) =>
            {
                if (current.Any(existing =>
                        string.Equals(existing.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) &&
                        ReferenceEquals(existing.Service, service)))
                {
                    throw new InvalidOperationException(
                        $"Plugin '{pluginId}' already exported this service instance for contract '{contractType.FullName}'.");
                }

                return current.Add(registration);
            });
    }

    private void RemoveExportsByPlugin(string pluginId)
    {
        foreach (var (contractType, exports) in _exports)
        {
            if (exports.Length == 0)
                continue;

            var filtered = exports
                .Where(export => !string.Equals(export.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
                .ToImmutableArray();

            if (filtered.Length == exports.Length)
                continue;

            if (filtered.IsEmpty)
            {
                _exports.TryRemove(contractType, out _);
                continue;
            }

            _exports[contractType] = filtered;
        }
    }

    private static RuntimePluginDescriptor ToDescriptor(InstalledPluginRecord record) =>
        new(record.PluginId, record.DisplayName, record.AssemblyPath, record.EntryTypeName, record.State);

    private static async Task SafeStopAsync(IHostManagedPlugin plugin, CancellationToken cancellationToken)
    {
        try
        {
            await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            switch (plugin)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }
}
