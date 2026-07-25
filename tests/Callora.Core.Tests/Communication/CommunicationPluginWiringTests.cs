using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Persistence;
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
}

internal sealed class CapturingHostPluginContext(bool hasDbFactory) : IHostPluginContext, IServiceProvider
{
    public Dictionary<Type, object> Exports { get; } = [];

    public IServiceProvider Services => this;

    public void Export(Type contractType, object service) => Exports[contractType] = service;

    public object? GetService(Type serviceType) =>
        hasDbFactory && serviceType == typeof(IPluginDbContextFactory<CommunicationDbContext>)
            ? new NoopMigrateDbContextFactory()
            : null;
}

internal sealed class NoopMigrateDbContextFactory : IPluginDbContextFactory<CommunicationDbContext>
{
    public CommunicationDbContext CreateDbContext() => throw new NotSupportedException("Wiring-Test benötigt keine echte DB.");

    public Task MigrateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
