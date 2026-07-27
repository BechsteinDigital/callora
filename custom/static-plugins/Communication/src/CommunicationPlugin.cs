using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Mcp.Contracts;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Application.Admin.Calls;
using Callora.Plugin.Communication.Application.Admin.SipAccounts;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Application.Compliance;
using Callora.Plugin.Communication.Application.Mcp;
using Callora.Plugin.Communication.Infrastructure.Capabilities;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Plugin.Communication.Infrastructure.Persistence;
using Callora.Plugin.Communication.Infrastructure.Persistence.Stores;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Plugin.Communication;

/// <summary>
/// First-party System-Tier communication foundation. Composition Root: exports the operator control
/// surface (Admin API) and the channel registry unconditionally; with a database it also runs GDPR
/// purge and the media WebSocket surface, and — when voice is enabled for the deployment — provisions a
/// live voice channel per enabled SIP account. Voice turns on either when the host injects an
/// <see cref="ISdkVoiceRuntime"/> (tests/custom hosts) or when configuration sets
/// <c>Communication:Voice:Enabled=true</c>, in which case the plugin builds the real SDK voice client itself.
/// </summary>
public sealed class CommunicationPlugin : IHostManagedPlugin
{
    /// <summary>Stable plugin identifier.</summary>
    public const string Id = "communication";

    /// <inheritdoc />
    public string PluginId => Id;

    /// <inheritdoc />
    public string DisplayName => "Communication";

    // The channel registry is host-provided runtime state (no persistence), so it lives for the
    // plugin's lifetime and is cleared on stop/unload.
    private readonly CommunicationChannelRegistry _channelRegistry = new();

    // Set during StartAsync when the media/voice surface is wired; torn down on stop.
    private SdkCallAudioRegistrar? _audioRegistrar;
    private VoiceChannelProvisioner? _voiceProvisioner;
    private CommunicationRuntimeCapabilitySource? _capabilitySource;

    // Call-control primitive, exported for in-process consumers (and the REST adapter); set when the
    // plugin has a database (it records call history). Disposed on stop so no call handler dangles.
    private CallControlService? _callControlService;

    // Observes inbound calls on every registered channel → call.ringing + history + lifecycle. Set
    // alongside the call-control service; detaches on stop.
    private IncomingCallObserver? _incomingCallObserver;

    // Only set when the plugin builds the voice client itself (config-enabled path). The plugin owns
    // its lifecycle and disposes it on stop; an injected runtime is owned by the host, not disposed here.
    private CalloraVoipSdk.IVoipClient? _ownedVoipClient;

    /// <summary>Configuration key that enables the plugin's self-built SDK voice client.</summary>
    internal const string VoiceEnabledConfigKey = "Communication:Voice:Enabled";

    /// <inheritdoc />
    public async ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dbContextFactory = context.Services.GetService(typeof(IPluginDbContextFactory<CommunicationDbContext>))
            as IPluginDbContextFactory<CommunicationDbContext>;
        var dataProtector = context.Services.GetService(typeof(IPluginDataProtector)) as IPluginDataProtector;

        // Call-control primitive: the neutral seam for placing/controlling calls plus call.* events and
        // call history. Built first (when a database is present — it records history) so its REST routes
        // can join the admin surface below; also exported for in-process plugins (Dialer/PBX/CRM).
        IReadOnlyList<HostAdminApiRouteRegistration> callRoutes = [];
        if (dbContextFactory is not null)
        {
            _callControlService = new CallControlService(
                _channelRegistry,
                new EfCallLogStore(dbContextFactory),
                context.Services.GetService(typeof(IBusinessEventBus)) as IBusinessEventBus,
                ResolveLogger<CallControlService>(context.Services),
                TimeProvider.System);
            context.Export<ICallControlService>(_callControlService);
            callRoutes = CallAdminRoutes.Build(_callControlService);

            // Contribute the call-control primitives as MCP tools so out-of-process AI agents can place
            // and control calls over the host's /mcp surface — the same service, an additional face. The
            // host owns transport, auth, workspace scope and permission enforcement; the plugin supplies
            // only the tools.
            context.Export<IMcpToolContributor>(new CommunicationMcpToolContributor(_callControlService));

            // Observe inbound calls on every channel (present and future) → call.ringing + history +
            // lifecycle. Started now so it catches channels as voice provisioning registers them below.
            // It never answers or routes — that is a consumer plugin's (PBX) decision.
            _incomingCallObserver = new IncomingCallObserver(_channelRegistry, _callControlService);
            _incomingCallObserver.Start();
        }

        // Operator Admin-API: the status route always; the SIP-account management routes only when
        // persistence and a data protector are present (credentials must be protectable) plus the
        // call-control routes above. These read/write the DB that this same StartAsync migrates below.
        IReadOnlyList<HostAdminApiRouteRegistration> accountRoutes =
            dbContextFactory is not null && dataProtector is not null
                ? SipAccountAdminRoutes.Build(new EfSipAccountStore(dbContextFactory), dataProtector, Id)
                : [];
        context.Export<IHostAdminApiExtensionContributor>(
            new CommunicationAdminApiExtensionContributor([.. accountRoutes, .. callRoutes]));

        // The channel registry is where the voice bridge registers channels and consumers resolve
        // them; exported unconditionally since it needs no database.
        context.Export<ICommunicationChannelRegistry>(_channelRegistry);

        // Runtime-capability source: derives communication.voice honestly from live channel health.
        // Exported unconditionally (it just observes the registry — empty until channels register); the
        // host registers it into its runtime-capability registry via the plugin's IRuntimeCapabilitySource export.
        _capabilitySource = new CommunicationRuntimeCapabilitySource(_channelRegistry);
        context.Export<IRuntimeCapabilitySource>(_capabilitySource);

        // Persistenz: eigenes Schema migrieren + GDPR-Purge-Contributor exportieren — nur wenn der
        // Host die DB-Factory bereitstellt (ein minimaler Host ohne Persistenz degradiert sauber).
        if (dbContextFactory is null)
        {
            return;
        }

        await dbContextFactory.MigrateAsync(cancellationToken).ConfigureAwait(false);

        context.Export<IWorkspaceDataPurgeContributor>(new CommunicationDataPurgeContributor(
            new CommunicationWorkspaceDataPurger(dbContextFactory)));

        // Media WebSocket surface (/ws/communication/media/{connectToken}) backed by the live-call
        // audio provider; the registrar populates it as tracked calls connect.
        var audioStreamProvider = new SdkCallAudioStreamProvider();
        _audioRegistrar = new SdkCallAudioRegistrar(
            audioStreamProvider, ResolveLogger<SdkCallAudioRegistrar>(context.Services));
        context.Export<IHostWebSocketEndpointContributor>(new CommunicationMediaWebSocketContributor(
            new EfMediaStreamSessionStore(dbContextFactory), audioStreamProvider));

        await ProvisionVoiceChannelsAsync(context, dbContextFactory, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        // Deregister and dispose provisioned channels, release live audio streams, then drop all
        // channel registrations so nothing dangles past unload.
        _voiceProvisioner?.Teardown();
        if (_audioRegistrar is not null)
        {
            await _audioRegistrar.ClearAsync().ConfigureAwait(false);
        }

        // Stop observing inbound calls before disposing the service it feeds.
        _incomingCallObserver?.Dispose();
        _callControlService?.Dispose();
        _capabilitySource?.Dispose();

        // Dispose the voice client only if the plugin built it (config-enabled path); an injected
        // runtime's client belongs to the host. Done after teardown so no channel outlives its line.
        _ownedVoipClient?.Dispose();

        _channelRegistry.Clear();
    }

    // Voice provisioning is opt-in: it needs the plugin data protector (to resolve credentials) and a
    // voice runtime — either injected by the host or built by the plugin when voice is configured.
    // Without both the plugin serves the foundation surface only — no voice channels.
    private async Task ProvisionVoiceChannelsAsync(
        IHostPluginContext context,
        IPluginDbContextFactory<CommunicationDbContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        if (context.Services.GetService(typeof(IPluginDataProtector)) is not IPluginDataProtector dataProtector)
        {
            return;
        }

        var voiceRuntime = ResolveVoiceRuntime(context.Services);
        if (voiceRuntime is null)
        {
            return;
        }

        var connector = new SdkVoiceChannelConnector(
            new SdkSipAccountFactory(dataProtector, Id),
            voiceRuntime,
            Id,
            ResolveLogger<SdkVoiceChannelConnector>(context.Services));
        _voiceProvisioner = new VoiceChannelProvisioner(
            connector, _channelRegistry, _audioRegistrar!, ResolveLogger<VoiceChannelProvisioner>(context.Services));

        var enabledAccounts = await new EfSipAccountStore(dbContextFactory)
            .ListEnabledAsync(cancellationToken)
            .ConfigureAwait(false);
        await _voiceProvisioner.ProvisionAsync(enabledAccounts, cancellationToken).ConfigureAwait(false);
    }

    // An explicitly injected runtime (tests/custom hosts) always wins. Otherwise, when the deployment
    // enables voice via configuration, the plugin builds and owns the real SDK voice client itself.
    internal ISdkVoiceRuntime? ResolveVoiceRuntime(IServiceProvider services)
    {
        if (services.GetService(typeof(ISdkVoiceRuntime)) is ISdkVoiceRuntime injected)
        {
            return injected;
        }

        if (!IsVoiceEnabled(services))
        {
            return null;
        }

        var options = VoiceClientOptions.FromConfiguration(services.GetService(typeof(IConfiguration)) as IConfiguration);
        _ownedVoipClient = HeadlessVoipClientFactory.Create(options);
        return new VoipClientVoiceRuntime(_ownedVoipClient);
    }

    // Reads the deployment switch from host configuration; absent/unparseable means voice stays off.
    internal static bool IsVoiceEnabled(IServiceProvider services) =>
        services.GetService(typeof(IConfiguration)) is IConfiguration configuration
        && bool.TryParse(configuration[VoiceEnabledConfigKey], out var enabled)
        && enabled;

    private static ILogger<T> ResolveLogger<T>(IServiceProvider services) =>
        services.GetService(typeof(ILogger<T>)) as ILogger<T> ?? NullLogger<T>.Instance;
}
