using Callora.Contracts.Communication;
using Callora.Host.PluginContracts.Application.Data;
using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Host.PluginContracts.Domain.Plugins;
using Callora.Plugins.Dialer.Application.Admin;
using Callora.Plugins.Dialer.Application.Numbers;
using Callora.Plugins.Dialer.Application.Runs;

namespace Callora.Plugins.Dialer.Application;

/// <summary>
/// Reference plugin proving the communication contract layer: it dials
/// workspace numbers over any voice channel resolved through
/// <see cref="ICommunicationChannelRegistry"/> without knowing SIP or the
/// providing plugin.
/// </summary>
public sealed class DialerPlugin : IHostManagedPlugin
{
    public const string Id = "dialer";

    private DialRunTracker? _runTracker;

    public string PluginId => Id;

    public string DisplayName => "Callora Dialer";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dataStore = ResolveRequired<IPluginDataStore>(context.Services);
        var channelRegistry = ResolveRequired<ICommunicationChannelRegistry>(context.Services);

        var numberStore = new DataStoreDialNumberStore(dataStore);
        var executor = new DialRunExecutor(channelRegistry);
        _runTracker = new DialRunTracker(executor, numberStore);

        context.Export<IHostAdminApiExtensionContributor>(
            new DialerAdminApiExtensionContributor(numberStore, _runTracker));

        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_runTracker is not null)
        {
            await _runTracker.DisposeAsync().ConfigureAwait(false);
            _runTracker = null;
        }
    }

    private static TService ResolveRequired<TService>(IServiceProvider services)
        where TService : class
    {
        return services.GetService(typeof(TService)) as TService
            ?? throw new InvalidOperationException(
                $"Host service '{typeof(TService).Name}' is required by the dialer plugin.");
    }
}
