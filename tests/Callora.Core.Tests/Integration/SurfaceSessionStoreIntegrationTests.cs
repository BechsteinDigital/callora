using Callora.Core.Application.Surfaces;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Proves the surface-session store against a real Postgres (ADR-017 §8.1): the
/// point of storing sessions server-side is that they can be withdrawn, so the
/// revocation and purge paths are the ones that must actually work.
/// </summary>
[Trait("Category", "Slow")]
[Collection(PostgresCollection.Name)]
public sealed class SurfaceSessionStoreIntegrationTests(PostgresFixture postgres)
{

    // Eine Datenbank je TEST, nicht je Aufruf: xUnit erzeugt die Klasse für jede
    // Testmethode neu, also ist dieses Feld pro Test frisch. Ohne das bekäme jeder
    // Kontext innerhalb eines Tests eine eigene Datenbank — was ein Test, der zwei
    // gleichzeitige Verbindungen gegeneinander laufen lässt, sofort bemerkt: Der
    // Schreiber landet in der einen, die Leser suchen in der anderen.
    private string? _database;

    private async Task<string> DatabaseAsync() =>
        _database ??= await postgres.CreateDatabaseAsync();
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task Session_RoundTripsWithItsClaims()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSurfaceSessionStore(context);
        var session = Session(claims: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["crm.roles"] = ["agent", "supervisor"],
        });

        await store.CreateAsync(session);
        var loaded = await store.GetAsync(session.SessionId);

        Assert.Equal(session.Subject, loaded!.Subject);
        Assert.Equal("crm.example", loaded.Subject.Issuer);
        Assert.Equal(["agent", "supervisor"], loaded.Identity.Claims["crm.roles"]);
        Assert.Equal("crm", loaded.IdentityPluginId);
    }

    [SkippableFact]
    public async Task Touching_RecordsUseWithoutExtendingTheExpiry()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSurfaceSessionStore(context);
        var session = Session();
        await store.CreateAsync(session);

        await store.TouchAsync(session.SessionId, Now.AddMinutes(30));

        var loaded = await store.GetAsync(session.SessionId);
        Assert.Equal(session.ExpiresAtUtc, loaded!.ExpiresAtUtc);
    }

    [SkippableFact]
    public async Task RevokingBySurface_EndsExactlyThatSurfacesSessions()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSurfaceSessionStore(context);
        var portal = Session(surfaceKey: "portal");
        var shop = Session(surfaceKey: "shop");
        await store.CreateAsync(portal);
        await store.CreateAsync(shop);

        var revoked = await store.RevokeForSurfaceAsync("workspace-a", "portal");

        Assert.Equal(1, revoked);
        Assert.Null(await store.GetAsync(portal.SessionId));
        Assert.NotNull(await store.GetAsync(shop.SessionId));
    }

    [SkippableFact]
    public async Task PurgingDropsExpiredSessionsOnly()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSurfaceSessionStore(context);
        var live = Session(expiresAtUtc: Now.AddHours(2));
        var stale = Session(expiresAtUtc: Now.AddMinutes(-1));
        await store.CreateAsync(live);
        await store.CreateAsync(stale);

        var purged = await store.PurgeExpiredAsync(Now);

        Assert.Equal(1, purged);
        Assert.NotNull(await store.GetAsync(live.SessionId));
        Assert.Null(await store.GetAsync(stale.SessionId));
    }

    private async Task<HostPersistenceDbContext> ContextAsync()
    {
        var context = new HostPersistenceDbContext(
            new DbContextOptionsBuilder<HostPersistenceDbContext>()
                .UseNpgsql(await DatabaseAsync())
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static SurfaceSession Session(
        string surfaceKey = "portal",
        DateTimeOffset? expiresAtUtc = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? claims = null) =>
        new(
            Guid.NewGuid(),
            "tenant-a",
            "workspace-a",
            surfaceKey,
            "portal.example.de",
            new SurfaceSubject("crm.example", "lead-42"),
            new SurfaceIdentity(
                "Erika Muster",
                claims ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                "password",
                Now.AddMinutes(-1),
                expiresAtUtc ?? Now.AddHours(2)),
            Now,
            expiresAtUtc ?? Now.AddHours(2),
            "crm",
            "1.0.0");
}
