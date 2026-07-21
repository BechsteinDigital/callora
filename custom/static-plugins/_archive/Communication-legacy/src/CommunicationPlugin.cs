using Callora.Core.Application.Data.Contracts;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Http.Contracts;
using Callora.Core.Application.Media.Contracts;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Application.Channels;
using Callora.Plugin.Communication.Application.Flows;
using Callora.Plugin.Communication.Application.Persistence;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application;

/// <summary>
/// First-party voice plugin. Bridges the CalloraVoipSdk engine onto the
/// platform communication contracts and manages SIP account configuration.
/// </summary>
public sealed class CommunicationPlugin : IHostManagedPlugin
{
    public const string Id = "communication";

    private SipChannelManager? _channelManager;
    private IVoiceEngine? _engine;
    private VoipCallHub? _callHub;
    private VoipCallBusinessEventRelay? _businessEventRelay;
    private CallLogWriter? _callLogWriter;

    public string PluginId => Id;

    public string DisplayName => "Callora Communication";

    public async ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dataStore = ResolveRequired<IPluginDataStore>(context.Services);
        var dataProtector = ResolveRequired<IPluginDataProtector>(context.Services);
        // The Communication foundation OWNS the channel registry (REV2 §4.2/§10.1B):
        // it creates the instance and exports it so consuming plugins (e.g. Dialer)
        // resolve it cross-plugin, without the host knowing communication at all.
        var channelRegistry = new CommunicationChannelRegistry();
        context.Export<ICommunicationChannelRegistry>(channelRegistry);

        var loggerFactory = context.Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory;

        // Plugin-eigene EF-Datenbank (PLAT-260): Schema/Migrationen zuerst,
        // dann SIP-Accounts aus dem alten jsonb-Store einmalig übernehmen.
        var dbContextFactory = context.Services.GetService(typeof(IPluginDbContextFactory<VoipDbContext>))
            as IPluginDbContextFactory<VoipDbContext>;

        ISipAccountStore accountStore;
        if (dbContextFactory is not null)
        {
            await dbContextFactory.MigrateAsync(cancellationToken).ConfigureAwait(false);
            var efStore = new EfSipAccountStore(dbContextFactory, dataProtector);
            await new SipAccountJsonbImporter(
                    new DataStoreSipAccountStore(dataStore, dataProtector),
                    efStore,
                    loggerFactory?.CreateLogger("Callora.Voip.Persistence"))
                .ImportAsync(cancellationToken)
                .ConfigureAwait(false);
            accountStore = efStore;

            // Erase the plugin's own workspace data (plugin_communication schema)
            // when a workspace is purged (REV2 §14).
            context.Export<IWorkspaceDataPurgeContributor>(
                new CommunicationWorkspaceDataPurgeContributor(dbContextFactory));
        }
        else
        {
            // Fallback ohne Host-DB-Provider: alter jsonb-Store.
            accountStore = new DataStoreSipAccountStore(dataStore, dataProtector);
        }

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

        // Call-Events laufen über den Business-Event-Bus (PLAT-270): Flows und
        // Webhooks konsumieren sie dort generisch.
        context.Export<IBusinessEventProvider>(new CallBusinessEventProvider());
        var businessEventBus = context.Services.GetService(typeof(IBusinessEventBus)) as IBusinessEventBus;
        if (businessEventBus is not null)
        {
            _businessEventRelay = new VoipCallBusinessEventRelay(
                _callHub,
                businessEventBus,
                loggerFactory?.CreateLogger("Callora.Voip.Events"));
            _businessEventRelay.Attach();
        }

        // Beendete Calls als typisierte Entities protokollieren (PLAT-260).
        if (dbContextFactory is not null)
        {
            _callLogWriter = new CallLogWriter(
                _callHub,
                dbContextFactory,
                loggerFactory?.CreateLogger("Callora.Voip.Persistence"));
            _callLogWriter.Attach();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        _callLogWriter?.Dispose();
        _callLogWriter = null;

        _businessEventRelay?.Dispose();
        _businessEventRelay = null;

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
