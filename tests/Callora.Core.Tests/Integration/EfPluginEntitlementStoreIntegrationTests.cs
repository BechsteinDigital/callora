using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Proves the persistent entitlement store (plugin_entitlements) against a real
/// Postgres — in particular that ListAsync surfaces explicit rows across scopes
/// including revoked (IsEntitled = false) ones. Requires Docker; skipped when
/// unavailable.
/// </summary>
[Trait("Category", "Slow")]
public sealed class EfPluginEntitlementStoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private bool _started;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
            _started = true;
        }
        catch (Exception)
        {
            _started = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_started)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task ListAsync_ReturnsRowsAcrossScopes_IncludingRevoked()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(Options());
        await context.Database.EnsureCreatedAsync();
        var store = new EfPluginEntitlementStore(context, new BackendHostOptions());

        await store.SetEntitledAsync("acme.plugin", isEntitled: true);                                   // platform (manual default)
        await store.SetEntitledAsync("acme.plugin", isEntitled: true, tenantKey: "tenant-a", source: "marketplace");
        await store.SetEntitledAsync("acme.plugin", isEntitled: false, workspaceKey: "workspace-a");     // workspace revoke

        var list = await store.ListAsync();

        Assert.Equal(3, list.Count);
        Assert.Contains(list, x => x.WorkspaceKey is null && x.TenantKey is null && x.IsEntitled && x.Source == "manual");
        Assert.Contains(list, x => x.TenantKey == "tenant-a" && x.WorkspaceKey is null && x.IsEntitled && x.Source == "marketplace");
        // The persistent store keeps an explicit "not entitled" override row.
        Assert.Contains(list, x => x.WorkspaceKey == "workspace-a" && !x.IsEntitled && x.Source == "manual");
    }

    [SkippableFact]
    public async Task SetEntitled_UpdatesProvenance_LastWriterWins()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(Options());
        await context.Database.EnsureCreatedAsync();
        var store = new EfPluginEntitlementStore(context, new BackendHostOptions());

        // A marketplace grant, then a direct operator revoke of the same scope.
        await store.SetEntitledAsync("acme.plugin", isEntitled: true, workspaceKey: "workspace-a", source: "marketplace");
        await store.SetEntitledAsync("acme.plugin", isEntitled: false, workspaceKey: "workspace-a", source: "manual");

        var row = Assert.Single(await store.ListAsync());
        Assert.False(row.IsEntitled);
        Assert.Equal("manual", row.Source); // last writer recorded
    }

    [SkippableFact]
    public async Task ListAsync_EmptyStore_ReturnsNothing()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(Options());
        await context.Database.EnsureCreatedAsync();
        var store = new EfPluginEntitlementStore(context, new BackendHostOptions());

        Assert.Empty(await store.ListAsync());
    }

    private DbContextOptions<HostPersistenceDbContext> Options() =>
        new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
}
