using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Mcp.Contracts;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Abstractions.Conference;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Application.Admin.Calls;
using Callora.Plugin.Communication.Application.Admin.SipAccounts;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Application.Compliance;
using Callora.Plugin.Communication.Application.Conference;
using Callora.Plugin.Communication.Application.Mcp;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Infrastructure.Capabilities;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Plugin.Communication.Infrastructure.Persistence;
using Callora.Plugin.Communication.Infrastructure.Persistence.Stores;
using Callora.Plugin.Communication.Infrastructure.RealtimeMedia;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk;
using CalloraVoipSdk.WebRtc;
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
/// plugin-scoped <c>Voice:Enabled=true</c>, in which case the plugin builds the real SDK voice client itself.
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

    // The single path from a persisted account to a live channel (#110): startup and every
    // admin mutation go through it, so the runtime cannot drift from the database.
    private SipAccountRuntimeReconciler? _sipRuntimeReconciler;
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

    // Set during StartAsync when the WebRTC surface is wired; torn down on stop.
    private IWebRtcClient? _ownedWebRtcClient;
    private WebRtcChannelProvisioner? _webRtcProvisioner;

    // Real-time media provider (M1 port) + the conference SFU over it (M2). Built alongside the WebRTC
    // surface and exported as IConferenceService for cross-plugin consumers (videoconference, call-center).
    // The provider owns its SDK client; both are disposed on stop.
    private CalloraVoipSdkProvider? _conferenceMediaProvider;

    /// <summary>Configuration key that enables the plugin's self-built SDK voice client.</summary>
    internal const string VoiceEnabledConfigKey = "Voice:Enabled";

    /// <summary>Configuration key that enables the plugin's self-built WebRTC client.</summary>
    internal const string WebRtcEnabledConfigKey = "WebRtc:Enabled";

    /// <summary>Configuration key for how many days finished call history is kept.</summary>
    internal const string CallLogRetentionDaysConfigKey = "Retention:CallLogDays";

    /// <summary>
    /// Default call-history window. Long enough for billing disputes and support, short enough
    /// that a phone number is not kept indefinitely by default.
    /// </summary>
    internal static readonly TimeSpan DefaultCallLogRetention = TimeSpan.FromDays(90);

    /// <summary>
    /// Reads the configured call-history window. A missing, unparsable or non-positive value
    /// falls back to the default rather than disabling retention, because "keep forever" must be
    /// a deliberate choice, not a typo.
    /// </summary>
    internal static TimeSpan ResolveCallLogRetention(IConfiguration? pluginConfiguration)
    {
        var configured = pluginConfiguration?[CallLogRetentionDaysConfigKey];
        return int.TryParse(configured, out var days) && days > 0
            ? TimeSpan.FromDays(days)
            : DefaultCallLogRetention;
    }

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
            var callLogStore = new EfCallLogStore(dbContextFactory);
            _callControlService = new CallControlService(
                _channelRegistry,
                callLogStore,
                ResolveLogger<CallControlService>(context.Services),
                TimeProvider.System);
            context.Export<ICallControlService>(_callControlService);
            callRoutes = CallAdminRoutes.Build(_callControlService);

            // Call events are written to the outbox with the log change and delivered by this
            // job, so a bus outage delays them instead of losing them (#113).
            if (context.Services.GetService(typeof(IBusinessEventBus)) is IBusinessEventBus eventBus)
            {
                context.Export<IBackgroundJobHandler>(new CallEventOutboxDrainJobHandler(
                    callLogStore,
                    eventBus,
                    TimeProvider.System,
                    ResolveLogger<CallEventOutboxDrainJobHandler>(context.Services)));
                context.Export<IRecurringJobProvider>(new CallEventOutboxRecurringJobProvider());
            }

            // Call history carries the remote party's number, so it needs a bound (#117). The
            // window is deployment-wide from plugin configuration; per-workspace policy is not
            // implemented.
            context.Export<IBackgroundJobHandler>(new CallLogRetentionJobHandler(
                callLogStore,
                TimeProvider.System,
                ResolveCallLogRetention(context.PluginConfiguration),
                ResolveLogger<CallLogRetentionJobHandler>(context.Services)));
            context.Export<IRecurringJobProvider>(new CallLogRetentionRecurringJobProvider());

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

        // Live-call audio surface and the SIP runtime reconciler are built here, before the admin
        // routes, because those routes must reconcile the runtime on every mutation (#110). Both
        // degrade cleanly: no data protector or no voice runtime means no reconciler, and the
        // routes fall back to pure persistence.
        var audioStreamProvider = new SdkCallAudioStreamProvider();
        _audioRegistrar = new SdkCallAudioRegistrar(
            audioStreamProvider, ResolveLogger<SdkCallAudioRegistrar>(context.Services));
        _sipRuntimeReconciler = TryCreateSipRuntimeReconciler(context, dataProtector, dbContextFactory);

        // Operator Admin-API: the status route always; the SIP-account management routes only when
        // persistence and a data protector are present (credentials must be protectable) plus the
        // call-control routes above. These read/write the DB that this same StartAsync migrates below.
        IReadOnlyList<HostAdminApiRouteRegistration> accountRoutes =
            dbContextFactory is not null && dataProtector is not null
                ? SipAccountAdminRoutes.Build(
                    new EfSipAccountStore(dbContextFactory), dataProtector, Id, _sipRuntimeReconciler)
                : [];

        // The status route answers a real dependency aggregate rather than a constant (#112).
        var readinessProbe = new CommunicationReadinessProbe(
            _channelRegistry,
            dbContextFactory is null ? null : new EfSipAccountStore(dbContextFactory),
            IsWebRtcEnabled(context.PluginConfiguration));
        context.Export<IHostAdminApiExtensionContributor>(
            new CommunicationAdminApiExtensionContributor([.. accountRoutes, .. callRoutes], readinessProbe));

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

        // WebRTC surface: placed after the DB gate because IncomingCallObserver + CallControlService
        // (built above, when a DB is present) must exist before WebRtcVoiceChannel.IncomingCall can fire —
        // without them an inbound call would have no ringing event, no history, and no lifecycle.
        // Consequence: WebRTC requires a database (consistent with the SIP voice path). Without a DB the
        // plugin degrades cleanly: no minter or signalling contributor exported, no error.
        // The in-memory token store still needs no persistence; it is constructed here for wiring symmetry.
        if (IsWebRtcEnabled(context.PluginConfiguration))
        {
            var loggerFactory = context.Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            var webRtcOptions = WebRtcClientOptions.FromConfiguration(context.PluginConfiguration);
            _ownedWebRtcClient = HeadlessWebRtcClientFactory.Create(webRtcOptions, loggerFactory);

            // A channel is externally reachable when STUN/TURN is configured or the bind endpoint is
            // non-loopback — either ensures NAT traversal is possible for remote browsers. IsLoopback
            // covers both IPv4 (127.0.0.0/8) and IPv6 (::1).
            var externallyReachable = webRtcOptions.IceServers.Count > 0
                || !System.Net.IPAddress.IsLoopback(webRtcOptions.LocalEndPoint.Address);

            var tokenLifetime = TimeSpan.FromMinutes(2);
            _webRtcProvisioner = new WebRtcChannelProvisioner(
                _ownedWebRtcClient,
                _channelRegistry,
                Id,
                externallyReachable,
                ResolveLogger<WebRtcChannelProvisioner>(context.Services));
            var signalingStore = new WebRtcSignalingSessionStore(TimeProvider.System, tokenLifetime);
            var minter = new WebRtcSessionMinter(_webRtcProvisioner, signalingStore, tokenLifetime);
            context.Export<IWebRtcSessionMinter>(minter);
            context.Export<IHostWebSocketEndpointContributor>(
                new WebRtcSignalingContributor(
                    signalingStore,
                    signalingStore,
                    ResolveLogger<WebRtcSignalingWebSocketHandler>(context.Services)));

            // Conference SFU (M2) over the neutral media provider port (M1): a dedicated media provider —
            // its own SDK client, video enabled so conference peers carry camera tracks — plus the
            // ConferenceService over it. Exported as IConferenceService for cross-plugin consumers
            // (videoconference, call-center) via the same curated-export path as ICommunicationChannelRegistry.
            // ADR-016 places provider selection on a DI/configuration boundary, but the
            // composition root always built the SDK provider, so a host could not substitute one
            // (#117). A host-supplied IRealtimeMediaProvider now wins; only without one does the
            // plugin build and own the SDK provider itself.
            var conferencePeerOptions = ToConferencePeerOptions(webRtcOptions);
            var injectedMediaProvider =
                context.Services.GetService(typeof(IRealtimeMediaProvider)) as IRealtimeMediaProvider;
            if (injectedMediaProvider is null)
            {
                _conferenceMediaProvider = CalloraVoipSdkProvider.Create(conferencePeerOptions, loggerFactory);
            }

            var conferenceService = new ConferenceService(
                injectedMediaProvider ?? _conferenceMediaProvider!,
                conferencePeerOptions,
                ResolveLogger<ConferenceService>(context.Services));
            context.Export<IConferenceService>(conferenceService);
        }

        context.Export<IWorkspaceDataPurgeContributor>(new CommunicationDataPurgeContributor(
            new CommunicationWorkspaceDataPurger(dbContextFactory)));

        // Media WebSocket surface (/ws/communication/media/{connectToken}) backed by the live-call
        // audio provider built above; the registrar populates it as tracked calls connect.
        var mediaStreamSessionStore = new EfMediaStreamSessionStore(dbContextFactory);
        context.Export<IHostWebSocketEndpointContributor>(new CommunicationMediaWebSocketContributor(
            mediaStreamSessionStore, audioStreamProvider));

        // Spent and expired media tickets are swept hourly (#108); without this the
        // table only ever grows.
        context.Export<IBackgroundJobHandler>(new MediaStreamSessionPurgeJobHandler(mediaStreamSessionStore));
        context.Export<IRecurringJobProvider>(new MediaStreamSessionPurgeRecurringJobProvider());

        await ProvisionVoiceChannelsAsync(dbContextFactory, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        // Deregister and dispose provisioned channels, release live audio streams, then drop all
        // channel registrations so nothing dangles past unload.
        _sipRuntimeReconciler?.Dispose();
        if (_audioRegistrar is not null)
        {
            await _audioRegistrar.ClearAsync().ConfigureAwait(false);
        }

        // Stop observing inbound calls before disposing the service it feeds.
        _incomingCallObserver?.Dispose();
        if (_callControlService is not null)
        {
            // Finalizes calls still in progress rather than leaving them in-progress forever (#113).
            await _callControlService.DisposeAsync().ConfigureAwait(false);
        }

        _capabilitySource?.Dispose();

        // Dispose the voice client only if the plugin built it (config-enabled path); an injected
        // runtime's client belongs to the host. Done after teardown so no channel outlives its line.
        _ownedVoipClient?.Dispose();

        // WebRTC teardown: deregister channels first, then async-dispose the client.
        _webRtcProvisioner?.Teardown();
        if (_ownedWebRtcClient is not null)
        {
            await _ownedWebRtcClient.DisposeAsync().ConfigureAwait(false);
        }

        // Conference SFU teardown: dispose the media provider (which owns its SDK client). Any live
        // participant sessions are owned by the consuming vertical and disposed by it on socket close.
        if (_conferenceMediaProvider is not null)
        {
            await _conferenceMediaProvider.DisposeAsync().ConfigureAwait(false);
        }

        _channelRegistry.Clear();
    }

    // Voice provisioning is opt-in: it needs the plugin data protector (to resolve credentials) and a
    // voice runtime — either injected by the host or built by the plugin when voice is configured.
    // Without both the plugin serves the foundation surface only — no voice channels.
    private SipAccountRuntimeReconciler? TryCreateSipRuntimeReconciler(
        IHostPluginContext context,
        IPluginDataProtector? dataProtector,
        IPluginDbContextFactory<CommunicationDbContext>? dbContextFactory)
    {
        if (dataProtector is null)
        {
            return null;
        }

        var voiceRuntime = ResolveVoiceRuntime(context.Services, context.PluginConfiguration);
        if (voiceRuntime is null)
        {
            return null;
        }

        var connector = new SdkVoiceChannelConnector(
            new SdkSipAccountFactory(dataProtector, Id),
            voiceRuntime,
            Id,
            ResolveLogger<SdkVoiceChannelConnector>(context.Services));

        // Without a database there is no account row to write a status onto, so the reconciler
        // runs without a projector rather than with a no-op one (#112).
        var statusProjector = dbContextFactory is null
            ? null
            : new EfSipAccountStatusProjector(
                new EfSipAccountStore(dbContextFactory),
                TimeProvider.System,
                ResolveLogger<EfSipAccountStatusProjector>(context.Services));

        return new SipAccountRuntimeReconciler(
            connector,
            _channelRegistry,
            _audioRegistrar!,
            ResolveLogger<SipAccountRuntimeReconciler>(context.Services),
            statusProjector);
    }

    // Startup uses the same reconciler as the admin mutations, so there is one provisioning
    // path rather than two that can disagree (#110). A failure is written back onto the account,
    // so an operator sees why it is dark instead of a permanent "Connecting" (#111/#112).
    private async Task ProvisionVoiceChannelsAsync(
        IPluginDbContextFactory<CommunicationDbContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        if (_sipRuntimeReconciler is null)
        {
            return;
        }

        var store = new EfSipAccountStore(dbContextFactory);
        var enabledAccounts = await store.ListEnabledAsync(cancellationToken).ConfigureAwait(false);

        foreach (var account in enabledAccounts)
        {
            var result = await _sipRuntimeReconciler.ApplyAsync(account, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                continue;
            }

            // Accounts created before the unsupported-method guard existed live on in the
            // database; this is where they stop being invisible (#111).
            account.ReportStatus(SipAccountStatus.Failed, result.Error, DateTimeOffset.UtcNow);
            await store.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        }
    }

    // An explicitly injected runtime (tests/custom hosts) always wins. Otherwise, when the deployment
    // enables voice via configuration, the plugin builds and owns the real SDK voice client itself.
    internal ISdkVoiceRuntime? ResolveVoiceRuntime(
        IServiceProvider services,
        IConfiguration? pluginConfiguration = null)
    {
        if (services.GetService(typeof(ISdkVoiceRuntime)) is ISdkVoiceRuntime injected)
        {
            return injected;
        }

        pluginConfiguration ??= services.GetService(typeof(IConfiguration)) as IConfiguration;
        if (!IsVoiceEnabled(pluginConfiguration))
        {
            return null;
        }

        var options = VoiceClientOptions.FromConfiguration(pluginConfiguration);
        _ownedVoipClient = HeadlessVoipClientFactory.Create(options);
        return new VoipClientVoiceRuntime(_ownedVoipClient);
    }

    // Reads the deployment switch from host configuration; absent/unparseable means voice stays off.
    internal static bool IsVoiceEnabled(IServiceProvider services) =>
        IsVoiceEnabled(services.GetService(typeof(IConfiguration)) as IConfiguration);

    internal static bool IsVoiceEnabled(IConfiguration? pluginConfiguration) =>
        pluginConfiguration is not null
        && bool.TryParse(pluginConfiguration[VoiceEnabledConfigKey], out var enabled)
        && enabled;

    // Reads the WebRTC deployment switch from host configuration; absent/unparseable means WebRTC stays off.
    internal static bool IsWebRtcEnabled(IServiceProvider services) =>
        IsWebRtcEnabled(services.GetService(typeof(IConfiguration)) as IConfiguration);

    internal static bool IsWebRtcEnabled(IConfiguration? pluginConfiguration) =>
        pluginConfiguration is not null
        && bool.TryParse(pluginConfiguration[WebRtcEnabledConfigKey], out var enabled)
        && enabled;

    // Maps the deployment WebRTC options onto the neutral per-peer options the conference SFU builds its
    // peers with. Video is forced on (a conference carries camera tracks) regardless of the voice-oriented
    // config default; ICE servers and audio codecs carry over so conference peers reach remote browsers the
    // same way the voice channel does.
    internal static MediaPeerOptions ToConferencePeerOptions(WebRtcClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new MediaPeerOptions
        {
            AudioCodecs = options.AudioCodecs,
            VideoCodecs = options.VideoCodecs,
            EnableVideo = true,
            // Conference peers add one audio/video pair per remote participant after their initial offer.
            // Stable numeric MIDs keep every previously negotiated m-line at the same index during those
            // re-offers, as RFC 8829 and browser setRemoteDescription require.
            UseStableNumericMediaIds = true,
            IceServers = [.. options.IceServers.Select(ToMediaIceServer)],
            LocalEndPoint = options.LocalEndPoint,
        };
    }

    private static MediaIceServer ToMediaIceServer(IceServerConfiguration server) => new(
        server.Host,
        server.Port,
        server.Type.ToString().ToLowerInvariant(),
        server.Transport.ToString().ToLowerInvariant(),
        server.Username,
        server.Password);

    private static ILogger<T> ResolveLogger<T>(IServiceProvider services) =>
        services.GetService(typeof(ILogger<T>)) as ILogger<T> ?? NullLogger<T>.Instance;
}
