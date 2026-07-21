using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Infrastructure.Persistence;
using Xunit;

namespace Callora.Core.Tests.Communication;

/// <summary>
/// Composition-Wiring des Plugins (B3c): StartAsync exportiert den Admin-Contributor immer und —
/// wenn der Host die DB-Factory bereitstellt — zusätzlich den GDPR-Purge-Contributor (nach dem
/// Migrate); ohne DB-Factory degradiert es sauber (kein Crash, nur Admin).
/// </summary>
public sealed class CommunicationPluginWiringTests
{
    [Fact]
    public async Task StartAsync_WithDbFactory_ExportsAdminAndPurgeContributors()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: true);

        await new CommunicationPlugin().StartAsync(context);

        Assert.Contains(typeof(IHostAdminApiExtensionContributor), context.Exports.Keys);
        Assert.Contains(typeof(IWorkspaceDataPurgeContributor), context.Exports.Keys);
        Assert.Contains(typeof(IHostWebSocketEndpointContributor), context.Exports.Keys);
    }

    [Fact]
    public async Task StartAsync_WithoutDbFactory_ExportsOnlyAdmin_AndDoesNotThrow()
    {
        var context = new CapturingHostPluginContext(hasDbFactory: false);

        await new CommunicationPlugin().StartAsync(context);

        Assert.Contains(typeof(IHostAdminApiExtensionContributor), context.Exports.Keys);
        Assert.DoesNotContain(typeof(IWorkspaceDataPurgeContributor), context.Exports.Keys);
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
