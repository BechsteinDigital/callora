using Callora.Contracts.Communication;
using Callora.Host.PluginContracts.Application.Data;
using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Host.PluginContracts.Application.Secrets;
using Callora.Host.PluginContracts.Domain.Plugins;
using Callora.Plugins.Voip.Application.Accounts;
using Callora.Plugins.Voip.Application.Admin;
using Callora.Plugins.Voip.Application.Channels;
using Microsoft.Extensions.Logging;

namespace Callora.Plugins.Voip.Application;

/// <summary>
/// First-party voice plugin. Bridges the CalloraVoipSdk engine onto the
/// platform communication contracts and manages SIP account configuration.
/// </summary>
public sealed class VoipPlugin : IHostManagedPlugin
{
    public const string Id = "voip";

    private SipChannelManager? _channelManager;
    private IVoiceEngine? _engine;

    public string PluginId => Id;

    public string DisplayName => "Callora Voice";

    public async ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dataStore = ResolveRequired<IPluginDataStore>(context.Services);
        var dataProtector = ResolveRequired<IPluginDataProtector>(context.Services);
        var channelRegistry = ResolveRequired<ICommunicationChannelRegistry>(context.Services);

        var loggerFactory = context.Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory;

        var accountStore = new DataStoreSipAccountStore(dataStore, dataProtector);
        _engine = new VoipSdkVoiceEngine();
        _channelManager = new SipChannelManager(
            channelRegistry,
            _engine,
            accountStore,
            loggerFactory?.CreateLogger<SipChannelManager>());
        await _channelManager.SynchronizeAllAsync(cancellationToken).ConfigureAwait(false);

        context.Export<IHostAdminApiExtensionContributor>(
            new VoipAdminApiExtensionContributor(accountStore, _channelManager));
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_channelManager is not null)
        {
            await _channelManager.DisposeAsync().ConfigureAwait(false);
            _channelManager = null;
        }

        if (_engine is not null)
        {
            await _engine.DisposeAsync().ConfigureAwait(false);
            _engine = null;
        }
    }

    private static TService ResolveRequired<TService>(IServiceProvider services)
        where TService : class
    {
        return services.GetService(typeof(TService)) as TService
            ?? throw new InvalidOperationException(
                $"Host service '{typeof(TService).Name}' is required by the voice plugin.");
    }
}
