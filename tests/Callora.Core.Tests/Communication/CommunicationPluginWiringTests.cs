using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
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
