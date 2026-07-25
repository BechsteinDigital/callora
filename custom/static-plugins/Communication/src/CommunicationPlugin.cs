using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Application.Admin.SipAccounts;
using Callora.Plugin.Communication.Application.Compliance;
using Callora.Plugin.Communication.Infrastructure.Capabilities;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Plugin.Communication.Infrastructure.Persistence;
using Callora.Plugin.Communication.Infrastructure.Persistence.Stores;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Plugin.Communication;

/// <summary>
/// First-party System-Tier communication foundation. Composition Root: exports the operator control
/// surface (Admin API) and the channel registry unconditionally; with a database it also runs GDPR
/// purge and the media WebSocket surface, and — when the deployment supplies an
/// <see cref="ISdkVoiceRuntime"/> — provisions a live voice channel per enabled SIP account.
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

    /// <inheritdoc />
    public async ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dbContextFactory = context.Services.GetService(typeof(IPluginDbContextFactory<CommunicationDbContext>))
            as IPluginDbContextFactory<CommunicationDbContext>;
        var dataProtector = context.Services.GetService(typeof(IPluginDataProtector)) as IPluginDataProtector;

        // Operator Admin-API: the status route always; the SIP-account management routes only when
        // persistence and a data protector are present (credentials must be protectable). The account
        // routes read/write the DB that this same StartAsync migrates below.
        IReadOnlyList<HostAdminApiRouteRegistration> accountRoutes =
            dbContextFactory is not null && dataProtector is not null
                ? SipAccountAdminRoutes.Build(new EfSipAccountStore(dbContextFactory), dataProtector, Id)
                : [];
        context.Export<IHostAdminApiExtensionContributor>(new CommunicationAdminApiExtensionContributor(accountRoutes));

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

        _capabilitySource?.Dispose();
        _channelRegistry.Clear();
    }

    // Voice provisioning is opt-in: only when the deployment supplies an SDK voice runtime (a
    // configured SIP client) and the plugin data protector for resolving credentials. Without them
    // the plugin serves the foundation surface only — no voice channels.
    private async Task ProvisionVoiceChannelsAsync(
        IHostPluginContext context,
        IPluginDbContextFactory<CommunicationDbContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        if (context.Services.GetService(typeof(ISdkVoiceRuntime)) is not ISdkVoiceRuntime voiceRuntime
            || context.Services.GetService(typeof(IPluginDataProtector)) is not IPluginDataProtector dataProtector)
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

    private static ILogger<T> ResolveLogger<T>(IServiceProvider services) =>
        services.GetService(typeof(ILogger<T>)) as ILogger<T> ?? NullLogger<T>.Instance;
}
