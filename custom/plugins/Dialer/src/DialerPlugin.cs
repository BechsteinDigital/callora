using Callora.Plugin.Communication.Abstractions;
using Callora.Core.Application.Data.Contracts;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Callora.Plugins.Dialer.Application.Admin;
using Callora.Plugins.Dialer.Application.Numbers;
using Callora.Plugins.Dialer.Application.Runs;

namespace Callora.Plugins.Dialer.Application;

/// <summary>
/// Reference plugin proving the communication contract layer: it dials
/// workspace numbers over any voice channel resolved through
/// <see cref="ICommunicationChannelRegistry"/> without knowing SIP. Dial runs
/// execute as durable host background jobs and survive restarts.
/// </summary>
public sealed class DialerPlugin : IHostManagedPlugin
{
    public const string Id = "dialer";

    public string PluginId => Id;

    public string DisplayName => "Callora Dialer";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dataStore = ResolveRequired<IPluginDataStore>(context.Services);
        var channelRegistry = ResolveRequired<ICommunicationChannelRegistry>(context.Services);
        var jobQueue = ResolveRequired<IBackgroundJobQueue>(context.Services);

        var numberStore = new DataStoreDialNumberStore(dataStore);
        var runStore = new DataStoreDialRunStore(dataStore);
        var executor = new DialRunExecutor(channelRegistry);
        var coordinator = new DialRunCoordinator(runStore, jobQueue);

        context.Export<IBackgroundJobHandler>(new DialRunJobHandler(executor, numberStore, runStore));
        context.Export<IHostAdminApiExtensionContributor>(
            new DialerAdminApiExtensionContributor(numberStore, coordinator));

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    private static TService ResolveRequired<TService>(IServiceProvider services)
        where TService : class
    {
        return services.GetService(typeof(TService)) as TService
            ?? throw new InvalidOperationException(
                $"Host service '{typeof(TService).Name}' is required by the dialer plugin.");
    }
}
