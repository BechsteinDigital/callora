using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Abstractions.Conference;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Application.Flows;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Callora.Core.Tests.Communication;

/// <summary>
/// Composition-Wiring des Plugins: StartAsync exportiert den Admin-Contributor und die
/// Channel-Registry (persistenzfrei) immer und — wenn der Host die DB-Factory bereitstellt —
/// zusätzlich Purge- und WebSocket-Contributor (nach dem Migrate); ohne DB-Factory degradiert es
/// sauber (kein Crash, kein Purge/WS).
/// </summary>
public sealed class CommunicationPluginWiringTests
{
    [Fact]
    public async Task StartAsync_WithDbFactory_ExportsAllContributors()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        Assert.Contains(typeof(IHostAdminApiExtensionContributor), context.Exports.Keys);
        Assert.Contains(typeof(ICommunicationChannelRegistry), context.Exports.Keys);
        Assert.Contains(typeof(IWorkspaceDataPurgeContributor), context.Exports.Keys);
        Assert.Contains(typeof(IHostWebSocketEndpointContributor), context.Exports.Keys);
    }

    [Fact]
    public async Task StartAsync_ExportsCallAccess_AlongsideCallControl()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        // Both faces of the same tracked-call state: commands over DTOs, observation over the live
        // call. Exporting only one of them would leave the other unreachable for other plugins.
        Assert.Contains(typeof(ICallControlService), context.Exports.Keys);
        Assert.Contains(typeof(ICallAccess), context.Exports.Keys);
        Assert.Same(context.Exports[typeof(ICallControlService)], context.Exports[typeof(ICallAccess)]);
    }

    [Fact]
    public async Task StartAsync_WhenWebRtcEnabled_ExportsConferenceCallAttachment_AlongsideTheConferenceService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CommunicationPlugin.WebRtcEnabledConfigKey] = "true",
            })
            .Build();
        var context = new CapturingHostPluginContext(hasDbFactory: true, configuration: config);

        await new CommunicationPlugin().StartAsync(context);

        // The port a policy plugin like the SIP bridge resolves: without the export it can decide who
        // may enter a room but cannot get anyone in.
        Assert.Contains(typeof(IConferenceService), context.Exports.Keys);
        Assert.Contains(typeof(IConferenceCallAttachment), context.Exports.Keys);
    }

    [Fact]
    public async Task StartAsync_ExportsCallAudioPlayback()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        // Without this a dial-in can answer, collect a PIN and bridge the caller — but never tell them
        // any of it is happening.
        Assert.Contains(typeof(ICallAudioPlayback), context.Exports.Keys);
    }

    [Fact]
    public async Task StartAsync_ExportsTheQuotaRegistry_SoQuotasCanBeConfiguredAtAll()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        // The ledger is wired into the dial path, but a ledger nobody can configure limits nothing:
        // every origin stays unlimited and the whole thing is dead weight.
        Assert.Contains(typeof(ICallQuotaRegistry), context.Exports.Keys);
    }

    [Fact]
    public async Task StartAsync_ExportsTheCallJourney()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        // Every consumer that touches a call writes its own steps. Without the export, only
        // communication's own half of the story is ever recorded — which is the half that already had
        // a log line.
        Assert.Contains(typeof(ICallJourney), context.Exports.Keys);
    }

    [Fact]
    public async Task StartAsync_ExportsTheInboundNumberCatalog()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        // A consumer that owns particular numbers has to name them. Without this it can only offer a
        // free-text field, and a number typed in the wrong form simply never rings.
        Assert.Contains(typeof(IInboundNumberCatalog), context.Exports.Keys);
    }

    [Fact]
    public async Task StartAsync_ExportsDtmfCollector()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        // The receive half of the announcement mechanics: without it a dial-in can speak but not
        // listen.
        Assert.Contains(typeof(ICallDtmfCollector), context.Exports.Keys);
    }

    [Fact]
    public async Task StartAsync_WithoutCallControl_ExportsNoConferenceCallAttachment()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CommunicationPlugin.WebRtcEnabledConfigKey] = "true",
            })
            .Build();
        var context = new CapturingHostPluginContext(hasDbFactory: false, configuration: config);

        await new CommunicationPlugin().StartAsync(context);

        // Without call control there is no call to resolve, so the port could only ever fail. Offering
        // it anyway would read as a capability this deployment has.
        Assert.DoesNotContain(typeof(IConferenceCallAttachment), context.Exports.Keys);
    }

    [Fact]
    public async Task StartAsync_WithoutDbFactory_ExportsAdminAndRegistry_AndDoesNotThrow()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: false);

        await new CommunicationPlugin().StartAsync(context);

        Assert.Contains(typeof(IHostAdminApiExtensionContributor), context.Exports.Keys);
        Assert.Contains(typeof(ICommunicationChannelRegistry), context.Exports.Keys);
        Assert.DoesNotContain(typeof(IWorkspaceDataPurgeContributor), context.Exports.Keys);
    }

    [Fact]
    public async Task StartAsync_ExportsRuntimeCapabilitySource_WiredToChannelRegistry()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: false);

        await new CommunicationPlugin().StartAsync(context);

        // Exported unconditionally (no DB needed) so the host can register it into its runtime-capability registry.
        Assert.Contains(typeof(IRuntimeCapabilitySource), context.Exports.Keys);
        var source = (IRuntimeCapabilitySource)context.Exports[typeof(IRuntimeCapabilitySource)];
        var registry = (ICommunicationChannelRegistry)context.Exports[typeof(ICommunicationChannelRegistry)];
        Assert.Empty(source.CurrentGrants); // no channels registered yet

        // The source observes the very registry the plugin exported: a healthy voice channel grants voice.
        registry.Register("ws-1", new FakeVoiceChannel { ChannelId = "ch-1", Health = ChannelHealth.Up });

        Assert.Equal(
            [new RuntimeCapabilityGrant(CommunicationCapabilities.Voice, "ws-1")],
            source.CurrentGrants);
    }

    [Fact]
    public async Task StartAsync_WithDbFactory_ExposesTheMediaTicketRoute()
    {
        // The media socket used to have no production caller at all (#114): the endpoint existed,
        // but nothing minted the ticket that opens it.
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        Assert.True(
            context.AllExports.Any(e => e.ContractType == typeof(IMediaStreamSessionMinter)),
            "IMediaStreamSessionMinter should be exported for in-process consumers.");
        var contributor = (IHostAdminApiExtensionContributor)context.Exports[typeof(IHostAdminApiExtensionContributor)];
        Assert.Contains(
            contributor.Routes,
            r => r is { HttpMethod: "POST", RouteTemplate: "calls/{callId}/media-streams" });
    }

    [Fact]
    public async Task StartAsync_WithDbFactory_ContributesTheCallControlFlowActions()
    {
        // The flow actions existed only in the archived plugin, reaching for the provider's call
        // object; they are back over the call-control primitive (#116).
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        var actionTypes = context.AllExports
            .Where(e => e.ContractType == typeof(IFlowActionHandler))
            .Select(e => ((IFlowActionHandler)e.Service).Type)
            .ToArray();

        Assert.Equal(
            [
                CallFlowActionTypes.Accept,
                CallFlowActionTypes.Reject,
                CallFlowActionTypes.Hangup,
                CallFlowActionTypes.SendDtmf,
            ],
            actionTypes);
    }

    [Fact]
    public async Task StartAsync_WithoutDbFactory_ContributesNoFlowActions()
    {
        // Without call control there is no call for an action to act on.
        var context = new CapturingHostPluginContext(hasDbFactory: false);

        await new CommunicationPlugin().StartAsync(context);

        Assert.DoesNotContain(context.AllExports, e => e.ContractType == typeof(IFlowActionHandler));
    }

    [Fact]
    public async Task StartAsync_WithDbFactory_ExposesTheCallEventStream()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        Assert.Contains(context.AllExports, e => e.Service is CommunicationCallEventContributor);
        var contributor = (IHostAdminApiExtensionContributor)context.Exports[typeof(IHostAdminApiExtensionContributor)];
        Assert.Contains(
            contributor.Routes,
            r => r is { HttpMethod: "POST", RouteTemplate: "calls/event-stream" });
    }

    [Fact]
    public async Task StartAsync_WhenWebRtcDisabled_ExposesNoWebRtcSessionRoute()
    {
        // A route that can never succeed reads as a capability the deployment does not have.
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        var contributor = (IHostAdminApiExtensionContributor)context.Exports[typeof(IHostAdminApiExtensionContributor)];
        Assert.DoesNotContain(contributor.Routes, r => r.RouteTemplate == "webrtc/sessions");
    }

    [Fact]
    public async Task StartAsync_WhenWebRtcEnabled_ExposesTheWebRtcSessionRoute()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CommunicationPlugin.WebRtcEnabledConfigKey] = "true",
            })
            .Build();
        var context = new CapturingHostPluginContext(hasDbFactory: true, configuration: config);

        await new CommunicationPlugin().StartAsync(context);

        var contributor = (IHostAdminApiExtensionContributor)context.Exports[typeof(IHostAdminApiExtensionContributor)];
        Assert.Contains(
            contributor.Routes,
            r => r is { HttpMethod: "POST", RouteTemplate: "webrtc/sessions" });
    }

    [Fact]
    public async Task StartAsync_WhenWebRtcEnabled_ExportsMinterAndSignalingContributor()
    {
        // WebRTC requires a DB (IncomingCallObserver + CallControlService must exist for inbound calls).
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CommunicationPlugin.WebRtcEnabledConfigKey] = "true",
            })
            .Build();
        var context = new CapturingHostPluginContext(hasDbFactory: true, configuration: config);

        await new CommunicationPlugin().StartAsync(context);

        Assert.True(
            context.AllExports.Any(e => e.ContractType == typeof(IWebRtcSessionMinter)),
            "IWebRtcSessionMinter should be exported when WebRTC is enabled.");
        Assert.True(
            context.AllExports.Any(e => e.Service is WebRtcSignalingContributor),
            "A WebRtcSignalingContributor should be exported when WebRTC is enabled.");
    }

    [Fact]
    public async Task StartAsync_WhenWebRtcEnabled_ButNoDbFactory_DoesNotExportMinter()
    {
        // Without a DB, WebRTC degrades cleanly: no minter or signalling contributor, no throw.
        // Reason: IncomingCallObserver + CallControlService only exist when a DB is present; without them
        // a connected WebRTC peer would raise IncomingCall into a void (no ringing event, no history).
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CommunicationPlugin.WebRtcEnabledConfigKey] = "true",
            })
            .Build();
        var context = new CapturingHostPluginContext(hasDbFactory: false, configuration: config);

        var exception = await Record.ExceptionAsync(() => new CommunicationPlugin().StartAsync(context).AsTask());

        Assert.Null(exception);
        Assert.False(
            context.AllExports.Any(e => e.ContractType == typeof(IWebRtcSessionMinter)),
            "IWebRtcSessionMinter must not be exported when no DB factory is present.");
        Assert.False(
            context.AllExports.Any(e => e.Service is WebRtcSignalingContributor),
            "WebRtcSignalingContributor must not be exported when no DB factory is present.");
    }

    [Fact]
    public async Task StartAsync_WhenWebRtcDisabled_DoesNotExportMinter()
    {
        // No config → WebRTC disabled.
        var context = new CapturingHostPluginContext(hasDbFactory: false);

        await new CommunicationPlugin().StartAsync(context);

        Assert.False(
            context.AllExports.Any(e => e.ContractType == typeof(IWebRtcSessionMinter)),
            "IWebRtcSessionMinter must not be exported when WebRTC is disabled.");
        Assert.False(
            context.AllExports.Any(e => e.Service is WebRtcSignalingContributor),
            "WebRtcSignalingContributor must not be exported when WebRTC is disabled.");
    }
}

internal sealed class CapturingHostPluginContext(
    bool hasDbFactory,
    IConfiguration? configuration = null) : IHostPluginContext, IServiceProvider
{
    /// <summary>Last export per contract type (existing tests rely on this).</summary>
    public Dictionary<Type, object> Exports { get; } = [];

    /// <summary>Every export in registration order — supports multiple exports of the same contract type.</summary>
    public List<(Type ContractType, object Service)> AllExports { get; } = [];

    public IServiceProvider Services => this;

    public IConfiguration? PluginConfiguration => configuration;

    public void Export(Type contractType, object service)
    {
        Exports[contractType] = service;
        AllExports.Add((contractType, service));
    }

    public object? GetService(Type serviceType)
    {
        if (hasDbFactory && serviceType == typeof(IPluginDbContextFactory<CommunicationDbContext>))
        {
            return new NoopMigrateDbContextFactory();
        }

        if (serviceType == typeof(IConfiguration))
        {
            return configuration;
        }

        return null;
    }
}

internal sealed class NoopMigrateDbContextFactory : IPluginDbContextFactory<CommunicationDbContext>
{
    public CommunicationDbContext CreateDbContext() => throw new NotSupportedException("Wiring-Test benötigt keine echte DB.");

    public Task MigrateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
