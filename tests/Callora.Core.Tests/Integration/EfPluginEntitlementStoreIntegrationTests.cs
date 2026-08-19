using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Proves the persistent entitlement store (plugin_entitlements) against a real
/// Postgres — in particular that ListAsync surfaces explicit rows across scopes
/// including revoked (IsEntitled = false) ones. Requires Docker; skipped when
/// unavailable.
/// </summary>
[Trait("Category", "Slow")]
[Collection(PostgresCollection.Name)]
public sealed class EfPluginEntitlementStoreIntegrationTests(PostgresFixture postgres)
{

    // Eine Datenbank je TEST, nicht je Aufruf: xUnit erzeugt die Klasse für jede
    // Testmethode neu, also ist dieses Feld pro Test frisch. Ohne das bekäme jeder
    // Kontext innerhalb eines Tests eine eigene Datenbank — was ein Test, der zwei
    // gleichzeitige Verbindungen gegeneinander laufen lässt, sofort bemerkt: Der
    // Schreiber landet in der einen, die Leser suchen in der anderen.
    private string? _database;

    private async Task<string> DatabaseAsync() =>
        _database ??= await postgres.CreateDatabaseAsync();
    [SkippableFact]
    public async Task ListAsync_ReturnsRowsAcrossScopes_IncludingRevoked()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(await OptionsAsync());
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
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(await OptionsAsync());
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
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(await OptionsAsync());
        await context.Database.EnsureCreatedAsync();
        var store = new EfPluginEntitlementStore(context, new BackendHostOptions());

        Assert.Empty(await store.ListAsync());
    }

    private async Task<DbContextOptions<HostPersistenceDbContext>> OptionsAsync() =>
        new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;
}
