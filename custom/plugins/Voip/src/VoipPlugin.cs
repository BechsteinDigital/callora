using Callora.Contracts.Communication;
using Callora.Host.PluginContracts.Application.Data;
using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Host.PluginContracts.Application.Secrets;
using Callora.Host.PluginContracts.Domain.Plugins;
using Callora.Host.PluginContracts.Application.Webhooks;
using Callora.Host.PluginContracts.Application.Flows;
using Callora.Host.PluginContracts.Application.Http;
using Callora.Host.PluginContracts.Application.Media;
using Callora.Plugins.Voip.Application.Accounts;
using Callora.Plugins.Voip.Application.Admin;
using Callora.Plugins.Voip.Application.Calls;
using Callora.Plugins.Voip.Application.Channels;
using Callora.Plugins.Voip.Application.Flows;
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
    private VoipCallHub? _callHub;
    private VoipCallWebhookRelay? _webhookRelay;

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

        // Der komplette Call-Stack lebt im Plugin (PLAT-257): Hub, /api/calls-
        // Controller, Flow-Actions und der Webhook-Relay werden exportiert; der
        // Host konsumiert nur die Verträge.
        _callHub = new VoipCallHub(channelRegistry, loggerFactory?.CreateLogger("Callora.Voip.Calls"));
        _callHub.AttachToChannels();
        context.Export<ICallDirectory>(_callHub);
        context.Export<ICallEventStream>(_callHub);
        context.Export<IApiController>(new CallsController(_callHub, channelRegistry));

        context.Export<IFlowActionHandler>(new CallAcceptActionHandler(_callHub));
        context.Export<IFlowActionHandler>(new CallRejectActionHandler(_callHub));
        context.Export<IFlowActionHandler>(new CallHangupActionHandler(_callHub));
        var mediaLibrary = context.Services.GetService(typeof(IMediaLibrary)) as IMediaLibrary;
        if (mediaLibrary is not null)
        {
            context.Export<IFlowActionHandler>(new AudioPlayActionHandler(_callHub, mediaLibrary));
        }

        var webhookPublisher = context.Services.GetService(typeof(IWebhookEventPublisher)) as IWebhookEventPublisher;
        if (webhookPublisher is not null)
        {
            _webhookRelay = new VoipCallWebhookRelay(
                _callHub,
                webhookPublisher,
                loggerFactory?.CreateLogger("Callora.Voip.Webhooks"));
            _webhookRelay.Attach();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        _webhookRelay?.Dispose();
        _webhookRelay = null;

        if (_callHub is not null)
        {
            await _callHub.ShutdownAsync(cancellationToken).ConfigureAwait(false);
            _callHub = null;
        }

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
