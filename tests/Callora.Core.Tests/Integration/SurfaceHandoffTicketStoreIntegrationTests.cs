using Callora.Core.Application.Surfaces;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Single use is the whole point of a handoff ticket (ADR-017 §8.4), and single use
/// is a database property here. These run against a real Postgres because an
/// in-memory double cannot prove the delete-and-return actually races correctly.
/// </summary>
[Trait("Category", "Slow")]
[Collection(PostgresCollection.Name)]
public sealed class SurfaceHandoffTicketStoreIntegrationTests(PostgresFixture postgres)
{

    // Eine Datenbank je TEST, nicht je Aufruf: xUnit erzeugt die Klasse für jede
    // Testmethode neu, also ist dieses Feld pro Test frisch. Ohne das bekäme jeder
    // Kontext innerhalb eines Tests eine eigene Datenbank — was ein Test, der zwei
    // gleichzeitige Verbindungen gegeneinander laufen lässt, sofort bemerkt: Der
    // Schreiber landet in der einen, die Leser suchen in der anderen.
    private string? _database;

    private async Task<string> DatabaseAsync() =>
        _database ??= await postgres.CreateDatabaseAsync();
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task ATicketRoundTripsWithItsIdentity()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSurfaceHandoffTicketStore(context);
        var secret = SurfaceHandoffSecret.Create();

        await store.CreateAsync(Ticket(), SurfaceHandoffSecret.Hash(secret));
        var consumed = await store.ConsumeAsync(SurfaceHandoffSecret.Hash(secret));

        Assert.Equal("crm.example", consumed!.Subject.Issuer);
        Assert.Equal("lead-42", consumed.Subject.SubjectId);
        Assert.Equal(["agent"], consumed.Identity.Claims["crm.roles"]);
        Assert.Equal("meet.example.de", consumed.TargetAudience);
    }

    [SkippableFact]
    public async Task ConsumingTwiceYieldsTheTicketOnlyOnce()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSurfaceHandoffTicketStore(context);
        var secret = SurfaceHandoffSecret.Create();
        await store.CreateAsync(Ticket(), SurfaceHandoffSecret.Hash(secret));

        Assert.NotNull(await store.ConsumeAsync(SurfaceHandoffSecret.Hash(secret)));
        Assert.Null(await store.ConsumeAsync(SurfaceHandoffSecret.Hash(secret)));
    }

    [SkippableFact]
    public async Task TheSecretItselfIsNeverStored()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSurfaceHandoffTicketStore(context);
        var secret = SurfaceHandoffSecret.Create();

        await store.CreateAsync(Ticket(), SurfaceHandoffSecret.Hash(secret));

        var stored = await context.SurfaceHandoffTickets.AsNoTracking().SingleAsync();
        Assert.NotEqual(secret, stored.TokenHash);
        Assert.Equal(SurfaceHandoffSecret.Hash(secret), stored.TokenHash);
    }

    [SkippableFact]
    public async Task PurgingDropsExpiredTicketsOnly()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSurfaceHandoffTicketStore(context);
        var live = SurfaceHandoffSecret.Create();
        var stale = SurfaceHandoffSecret.Create();
        await store.CreateAsync(Ticket(expiresAtUtc: Now.AddMinutes(1)), SurfaceHandoffSecret.Hash(live));
        await store.CreateAsync(Ticket(expiresAtUtc: Now.AddSeconds(-1)), SurfaceHandoffSecret.Hash(stale));

        Assert.Equal(1, await store.PurgeExpiredAsync(Now));
        Assert.NotNull(await store.ConsumeAsync(SurfaceHandoffSecret.Hash(live)));
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

    private static SurfaceHandoffTicket Ticket(DateTimeOffset? expiresAtUtc = null) =>
        new(
            Guid.NewGuid(),
            "tenant-a",
            "workspace-a",
            "crm",
            "meet",
            "meet.example.de",
            new SurfaceSubject("crm.example", "lead-42"),
            new SurfaceIdentity(
                "Erika Muster",
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    ["crm.roles"] = ["agent"],
                },
                "password",
                Now.AddMinutes(-1),
                Now.AddHours(2)),
            Now,
            expiresAtUtc ?? Now.AddMinutes(1));
}
