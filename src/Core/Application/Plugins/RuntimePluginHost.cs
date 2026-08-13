using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

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

    /// <summary>
    /// Plugins, deren Exporte bereits eingesammelt wurden — der Sperrvermerk für späte Exporte.
    /// <para>
    /// Ein Plugin hält die Export-Delegate über seinen Kontext dauerhaft, und der Vertrag verbietet
    /// keinen späten Aufruf. Ohne diesen Vermerk landete ein Export NACH dem Einsammeln dauerhaft
    /// in der Tabelle: Kein weiterer Aufräumlauf kommt, also lieferten TryGetExport und GetExports
    /// danach eine Instanz aus einem entladenen Ladekontext. Die Marke wird zu Beginn jeder
    /// Aktivierung wieder gelöscht — sie sperrt den Nachzügler, nicht den Neustart.
    /// </para>
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _exportsRevoked = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _mutationLock = new(1, 1);
    private readonly SharedContractAssemblyRegistry _sharedContracts;
    private readonly RuntimeCapabilityRegistry? _runtimeCapabilities;
    private readonly PluginFaultRegistry? _faults;
    private readonly Callora.Core.Application.Mcp.Contracts.IMcpToolRegistry? _mcpTools;
    private long _exportSequence;
    private int _disposed;

    /// <summary>
    /// Creates a runtime plugin host.
    /// </summary>
    public RuntimePluginHost(
        IServiceProvider services,
        CalloraHostingOptions options,
        ILogger<RuntimePluginHost> logger,
        RuntimeCapabilityRegistry? runtimeCapabilities = null,
        PluginFaultRegistry? faults = null,
        Callora.Core.Application.Mcp.Contracts.IMcpToolRegistry? mcpTools = null)
    {
        _services = services;
        _options = options;
        _logger = logger;
        _sharedContracts = new SharedContractAssemblyRegistry(logger);
        _runtimeCapabilities = runtimeCapabilities;
        _faults = faults;
        _mcpTools = mcpTools;
    }

    /// <summary>
    /// Wird ausgelöst, wenn sich die Menge der aktiven Exports geändert hat — insbesondere
    /// unmittelbar nachdem die Exports eines Plugins zurückgezogen wurden und BEVOR sein
    /// Ladekontext entladen wird.
    /// </summary>
    /// <remarks>
    /// Für Verbraucher, die aus den Exports etwas Eigenes ableiten und festhalten (die
    /// Routing-Tabelle hält Delegaten auf Plugin-Methoden). Sie müssen ihre Ableitung an dieser
    /// Stelle fallen lassen, sonst hält sie den Ladekontext fest und das Entladen scheitert.
    /// Das Lifecycle-Ereignis genügt dafür nicht: Es kommt erst nach der Deaktivierung.
    /// </remarks>
    public event Action? ExportsChanged;

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
        {
            return Array.Empty<object>();
        }

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
        {
            return Array.Empty<CalloraPluginExport>();
        }

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
            {
                return inspection.Result;
            }

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
            if (!_installed.TryGetValue(pluginId, out var record))
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
                    // Dieselbe Buchführung wie in DeactivateAsync — sie fehlte hier, weil der
                    // Uninstall den internen Weg nimmt und der fasst den Zustand nie an. Bis
                    // hierher ist das Plugin aber schon aus _active heraus, gedraint, seine
                    // Exporte sind entfernt und StopAsync ist gelaufen: Es steht nur noch in
                    // _installed als Active. LoadedPlugins liest genau daraus, also meldeten
                    // Verfügbarkeitsprüfung und Installationsliste danach ein laufendes Plugin,
                    // das nicht mehr läuft.
                    record.State = RuntimePluginState.UnloadFailed;
                    _installed[record.PluginId] = record;
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
    /// <remarks>
    /// Idempotent, and that matters most when something else already went wrong. A host that fails
    /// during startup tears its container down along a path that can reach this twice; the second
    /// pass used to throw <see cref="ObjectDisposedException"/> on the already-disposed lock and
    /// replace the original failure with a meaningless one, which is expensive to diagnose exactly
    /// when diagnosis is what is needed.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

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
            RegisterDeclaredContracts(record.AssemblyPath, record.PluginId);
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

            // Der Sperrvermerk eines früheren Laufs fällt hier — und nicht erst nach StartAsync:
            // Das Plugin exportiert genau dort, und in _active steht es erst danach.
            _exportsRevoked.TryRemove(record.PluginId, out _);

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
            {
                await SafeStopAsync(plugin, cancellationToken).ConfigureAwait(false);
            }

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
            // Draining comes first and runs with the exports still in place, because work that is
            // still finishing may depend on them (ADR-018 §2.1).
            await DrainAsync(handle.Plugin, pluginId, cancellationToken).ConfigureAwait(false);
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
            // Hier wird bewusst NICHTS registriert. Contract-Assemblies landen im Default-ALC und
            // bleiben dort bis zum Prozessende — für ein Paket, das gleich darauf als
            // EntryPointNotFound oder inkompatibel abgelehnt wird, ist das eine dauerhafte Spur
            // von etwas, das nie installiert wurde: Ein späteres, echtes Plugin mit demselben
            // Contract in anderer Hauptversion prallt dann an einer Registrierung ab, die einem
            // abgelehnten Paket gehört. Die Inspektion braucht die Freigabe auch nicht — ihr
            // Ladekontext löst Contracts sonst plugin-lokal auf und wird im finally entladen.
            // Registriert wird beim Aktivieren (ActivateInternalAsync), und zwar mit Plugin-Id.
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
        {
            return null;
        }

        var hostVersion = typeof(IHostManagedPlugin).Assembly.GetName().Version;
        if (hostVersion is null)
        {
            return null;
        }

        if (pluginVersion.Major != hostVersion.Major)
        {
            return $"Plugin targets host contracts {pluginVersion}, but host uses {hostVersion}.";
        }

        return null;
    }

    /// <summary>
    /// The contract assemblies shared across plugin load contexts. Exposed so the catalog can
    /// report them and the dependency version gate can check against what plugins actually brought,
    /// not only against what the host itself ships.
    /// </summary>
    public SharedContractAssemblyRegistry SharedContracts => _sharedContracts;

    private void RegisterDeclaredContracts(string pluginAssemblyPath, string? declaringPluginId = null)
    {
        var declaredContracts = PluginContractManifestReader.ReadDeclaredContracts(pluginAssemblyPath);
        if (declaredContracts.Count == 0)
        {
            return;
        }

        var pluginDirectory = Path.GetDirectoryName(Path.GetFullPath(pluginAssemblyPath))
            ?? throw new InvalidOperationException($"Plugin path '{pluginAssemblyPath}' has no directory.");
        _sharedContracts.RegisterContracts(pluginDirectory, declaredContracts, declaringPluginId);
    }

    private static Type? ResolvePluginType(Assembly assembly, string? entryTypeName)
    {
        if (!string.IsNullOrWhiteSpace(entryTypeName))
        {
            return assembly.GetType(entryTypeName, throwOnError: false, ignoreCase: false);
        }

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

        var manuallyExported = GetExports(typeof(Callora.Core.Application.Http.Contracts.IApiController))
            .Select(static export => export.GetType())
            .ToHashSet();

        foreach (var controllerType in assemblyTypes)
        {
            if (controllerType.IsAbstract ||
                controllerType.IsInterface ||
                !typeof(Callora.Core.Application.Http.Contracts.IApiController).IsAssignableFrom(controllerType))
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
            RegisterExport(pluginId, typeof(Callora.Core.Application.Http.Contracts.IApiController), controller);

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

        // Ein Export nach dem Einsammeln bliebe für immer stehen. Abgewiesen wird er, nicht
        // geworfen: Der Aufrufer ist Plugin-Code, der beim Herunterfahren aus einem Timer oder
        // einer laufenden Aufgabe kommen kann — eine Ausnahme dorthin zu werfen macht aus einem
        // verspäteten Export einen Absturz beim Deaktivieren.
        if (_exportsRevoked.ContainsKey(pluginId) && !_active.ContainsKey(pluginId))
        {
            _logger.LogWarning(
                "Ignored export of '{ContractType}' from plugin {PluginId}: its exports were already withdrawn.",
                contractType.FullName,
                pluginId);
            return;
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

        // A plugin's runtime capability source is tracked so its conditional capabilities can be
        // derived from health. A failure here must not break the plugin's activation (fail-open for
        // the plugin, fail-closed for its conditional capabilities).
        if (contractType == typeof(IRuntimeCapabilitySource) && _runtimeCapabilities is not null)
        {
            try
            {
                _runtimeCapabilities.Register(pluginId, (IRuntimeCapabilitySource)service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register the runtime capability source of plugin {PluginId}.", pluginId);
            }
        }

        // A plugin's MCP tool contribution is added to the host's live tool collection so it is served
        // immediately on activation. A failure here must not break the plugin's activation (fail-open for
        // the plugin, fail-closed for its tools).
        if (contractType == typeof(Callora.Core.Application.Mcp.Contracts.IMcpToolContributor) && _mcpTools is not null)
        {
            try
            {
                _mcpTools.Register(pluginId, (Callora.Core.Application.Mcp.Contracts.IMcpToolContributor)service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register MCP tools of plugin {PluginId}.", pluginId);
            }
        }
    }

    private void RemoveExportsByPlugin(string pluginId)
    {
        // Erst der Vermerk, dann das Einsammeln: Andersherum bliebe genau das Fenster offen,
        // das hier geschlossen werden soll — ein Export zwischen letztem Filterlauf und Vermerk
        // wäre durchgerutscht und dauerhaft geblieben.
        _exportsRevoked[pluginId] = 0;

        foreach (var contractType in _exports.Keys)
        {
            // Lesen, filtern, zurückschreiben war drei Schritte weit vom Rest entfernt: Eine
            // Indexer-Zuweisung schreibt den gefilterten Stand auch dann, wenn dazwischen jemand
            // anderes etwas eingetragen hat, und macht dessen Eintrag wieder zunichte. Compare-
            // and-Swap mit Wiederholung schreibt nur auf den Stand, den wir gelesen haben.
            while (_exports.TryGetValue(contractType, out var exports))
            {
                if (exports.Length == 0)
                {
                    break;
                }

                var filtered = exports
                    .Where(export => !string.Equals(export.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
                    .ToImmutableArray();

                if (filtered.Length == exports.Length)
                {
                    break;
                }

                if (filtered.IsEmpty)
                {
                    if (_exports.TryRemove(new KeyValuePair<Type, ImmutableArray<PluginExportRegistration>>(contractType, exports)))
                    {
                        break;
                    }

                    continue;
                }

                if (_exports.TryUpdate(contractType, filtered, exports))
                {
                    break;
                }
            }
        }

        // Drop any runtime capability source the plugin registered (idempotent if it had none), so its
        // conditional capabilities immediately stop counting when it is deactivated.
        _runtimeCapabilities?.Unregister(pluginId);

        // Jeder, der aus den Exports etwas ABGELEITET hat, muss es JETZT wegwerfen — vor dem
        // Entladen, nicht danach. Die Routing-Tabelle etwa hält Delegaten auf Plugin-Methoden;
        // solange sie stehen, ist der Ladekontext angeheftet, und die Prüfung nach dem Unload
        // meldet UnloadFailed für ein Plugin, mit dem nichts verkehrt ist. Das
        // Lifecycle-Ereignis kommt für diesen Zweck zu spät: Es wird erst nach der Deaktivierung
        // veröffentlicht, also nach eben dieser Prüfung.
        ExportsChanged?.Invoke();

        // Die Fehlerhistorie geht mit: Wer ein Plugin neu aktiviert, hat in aller Regel die
        // Ursache behandelt — ein Budget aus der vorigen Fassung schlüge sonst sofort wieder zu
        // und ließe die neue wie die alte aussehen.
        _faults?.Clear(pluginId);

        // Withdraw the plugin's MCP tools from the live collection (idempotent if it had none), so they
        // immediately stop being advertised when it is deactivated.
        _mcpTools?.Deregister(pluginId);
    }

    private static RuntimePluginDescriptor ToDescriptor(InstalledPluginRecord record) =>
        new(record.PluginId, record.DisplayName, record.AssemblyPath, record.EntryTypeName, record.State);

    /// <summary>
    /// Gives a plugin that carries long-running work the chance to run dry before it is taken apart
    /// (ADR-018 §2.1). The host owns the deadline: an expired one is reported and the stop proceeds,
    /// because a drain may delay a deactivation but never prevent it. A plugin that does not
    /// implement the contract is unaffected.
    /// </summary>
    private async Task DrainAsync(IHostManagedPlugin plugin, string pluginId, CancellationToken cancellationToken)
    {
        if (plugin is not IDrainablePlugin drainable || _options.PluginDrainTimeout <= TimeSpan.Zero)
        {
            return;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.PluginDrainTimeout);

        try
        {
            await drainable.DrainAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Two distinct situations reach this point and an operator needs to tell them apart: the
            // caller pulling the plug ("stop now") versus a plugin that could not finish in time.
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Draining plugin {PluginId} was cancelled by the caller; stopping it now.",
                    pluginId);
            }
            else
            {
                _logger.LogWarning(
                    "Plugin {PluginId} did not finish draining within {DrainTimeout}; stopping it anyway.",
                    pluginId,
                    _options.PluginDrainTimeout);
            }
        }
        catch (Exception ex)
        {
            // A failed drain must not block the stop — the plugin is going away either way.
            _logger.LogWarning(ex, "Draining plugin {PluginId} failed; stopping it anyway.", pluginId);
            // Ein fehlgeschlagener Drain hinterlässt KEINEN Zustand: Das Plugin wird gestoppt
            // und sieht danach aus wie jedes andere inaktive. Ein Plugin, das seine laufende
            // Arbeit nie sauber beendet, schneidet aber bei jeder Deaktivierung Arbeit ab —
            // deshalb zählt es hier, wo es sonst spurlos bliebe. Eine gescheiterte AKTIVIERUNG
            // zählt bewusst nicht: Die führt zu Faulted und entzieht die Verfügbarkeit bereits
            // über RuntimeHealthy; sie ein zweites Mal zu zählen verlängerte nur die Sperre.
            _faults?.Record(pluginId, PluginFaultOrigin.Lifecycle);
        }
    }

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
