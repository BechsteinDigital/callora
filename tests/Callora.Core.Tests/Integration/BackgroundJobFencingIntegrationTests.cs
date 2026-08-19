using Callora.Core.Domain.Jobs;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Proves the job fencing token against a real Postgres: once a second worker
/// reclaims an expired lease, the previous owner's save is rejected instead of
/// overwriting the new owner (no split-brain double-write). Requires Docker;
/// skipped automatically when unavailable.
/// </summary>
[Trait("Category", "Slow")]
[Collection(PostgresCollection.Name)]
public sealed class BackgroundJobFencingIntegrationTests(PostgresFixture postgres)
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
    public async Task ReclaimedJob_RejectsPreviousOwnerSave()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;
        var lease = TimeSpan.FromMinutes(5);
        var t0 = DateTimeOffset.UtcNow;

        await using (var seed = new HostPersistenceDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.BackgroundJobs.Add(BackgroundJob.Create("t", "{}", t0, maxAttempts: 3, null, t0));
            await seed.SaveChangesAsync();
        }

        await using var contextA = new HostPersistenceDbContext(options);
        var storeA = new EfBackgroundJobStore(contextA);
        var jobA = await storeA.TryClaimNextDueAsync(t0, lease);
        Assert.NotNull(jobA);

        // A second worker reclaims after the lease has elapsed.
        await using (var contextB = new HostPersistenceDbContext(options))
        {
            var storeB = new EfBackgroundJobStore(contextB);
            var jobB = await storeB.TryClaimNextDueAsync(t0 + lease + TimeSpan.FromMinutes(1), lease);
            Assert.NotNull(jobB);
            Assert.Equal(jobA!.Id, jobB!.Id);
        }

        // The original owner can no longer persist its result.
        jobA!.MarkSucceeded(DateTimeOffset.UtcNow);
        var saved = await storeA.SaveAsync(jobA);

        Assert.False(saved);
    }
}
