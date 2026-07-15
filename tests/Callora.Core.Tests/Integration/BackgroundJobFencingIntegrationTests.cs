using Callora.Core.Domain.Jobs;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Proves the job fencing token against a real Postgres: once a second worker
/// reclaims an expired lease, the previous owner's save is rejected instead of
/// overwriting the new owner (no split-brain double-write). Requires Docker;
/// skipped automatically when unavailable.
/// </summary>
[Trait("Category", "Slow")]
public sealed class BackgroundJobFencingIntegrationTests : IAsyncLifetime
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
    public async Task ReclaimedJob_RejectsPreviousOwnerSave()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
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
