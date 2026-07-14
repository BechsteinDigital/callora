using Callora.Host.Backend.Application.Jobs;
using Callora.Host.Backend.Domain.Jobs;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Jobs;

/// <summary>
/// Verifies the lease-and-reclaim model that recovers jobs orphaned by a
/// crashed worker (P0-3). Exercised through <see cref="InMemoryBackgroundJobStore"/>,
/// whose claim predicate mirrors the EF store.
/// </summary>
public sealed class BackgroundJobLeaseTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    [Fact]
    public void MarkRunning_SetsLeaseExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var job = BackgroundJob.Create("t", "{}", now, 3, null, now);

        job.MarkRunning(now, Lease);

        Assert.Equal(BackgroundJobStatus.Running, job.Status);
        Assert.Equal(now + Lease, job.LeaseExpiresAtUtc);
    }

    [Fact]
    public void MarkSucceeded_ReleasesLease()
    {
        var now = DateTimeOffset.UtcNow;
        var job = BackgroundJob.Create("t", "{}", now, 3, null, now);
        job.MarkRunning(now, Lease);

        job.MarkSucceeded(now);

        Assert.Null(job.LeaseExpiresAtUtc);
    }

    [Fact]
    public void MarkFailedAttempt_ReleasesLease()
    {
        var now = DateTimeOffset.UtcNow;
        var job = BackgroundJob.Create("t", "{}", now, 3, null, now);
        job.MarkRunning(now, Lease);

        job.MarkFailedAttempt("boom", TimeSpan.FromSeconds(1), now);

        Assert.Null(job.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task TryClaim_WhileLeaseValid_DoesNotReclaim()
    {
        var store = new InMemoryBackgroundJobStore();
        var now = DateTimeOffset.UtcNow;
        await store.AddAsync(BackgroundJob.Create("t", "{}", now, 3, null, now));

        var first = await store.TryClaimNextDueAsync(now, Lease);
        Assert.NotNull(first);

        // A competing worker one minute later must not steal the still-leased job.
        var second = await store.TryClaimNextDueAsync(now.AddMinutes(1), Lease);
        Assert.Null(second);
    }

    [Fact]
    public async Task TryClaim_AfterLeaseExpiry_ReclaimsOrphanedRunningJob()
    {
        var store = new InMemoryBackgroundJobStore();
        var now = DateTimeOffset.UtcNow;
        await store.AddAsync(BackgroundJob.Create("t", "{}", now, 3, null, now));

        var first = await store.TryClaimNextDueAsync(now, Lease);
        Assert.NotNull(first);
        Assert.Equal(1, first!.AttemptCount);

        // Worker crashed: job stuck Running, lease elapsed. It must be reclaimed.
        var reclaimed = await store.TryClaimNextDueAsync(now + Lease + TimeSpan.FromSeconds(1), Lease);

        Assert.NotNull(reclaimed);
        Assert.Equal(first.Id, reclaimed!.Id);
        Assert.Equal(2, reclaimed.AttemptCount);
    }

    [Fact]
    public async Task TryClaim_ExhaustedExpiredJob_IsNotReclaimed()
    {
        var store = new InMemoryBackgroundJobStore();
        var now = DateTimeOffset.UtcNow;
        await store.AddAsync(BackgroundJob.Create("t", "{}", now, maxAttempts: 1, null, now));

        var claimed = await store.TryClaimNextDueAsync(now, Lease);
        Assert.NotNull(claimed);
        Assert.Equal(1, claimed!.AttemptCount); // == MaxAttempts

        // Lease expired but the attempt budget is spent: no endless reclaim.
        var reclaimed = await store.TryClaimNextDueAsync(now + Lease + TimeSpan.FromSeconds(1), Lease);
        Assert.Null(reclaimed);
    }

    [Fact]
    public async Task FailExpiredExhausted_MarksExhaustedExpiredJobFailed()
    {
        var store = new InMemoryBackgroundJobStore();
        var now = DateTimeOffset.UtcNow;
        await store.AddAsync(BackgroundJob.Create("t", "{}", now, maxAttempts: 1, null, now));
        var claimed = await store.TryClaimNextDueAsync(now, Lease);
        Assert.NotNull(claimed);

        var failed = await store.FailExpiredExhaustedAsync(now + Lease + TimeSpan.FromSeconds(1));

        Assert.Equal(1, failed);
        Assert.Equal(BackgroundJobStatus.Failed, claimed!.Status);
    }

    [Fact]
    public async Task HasActiveJob_ExpiredLeaseRunning_NotCountedActive()
    {
        var store = new InMemoryBackgroundJobStore();
        var now = DateTimeOffset.UtcNow;
        await store.AddAsync(BackgroundJob.Create("t", "{}", now, 3, null, now));
        Assert.NotNull(await store.TryClaimNextDueAsync(now, Lease));

        // Valid lease blocks re-enqueue; an expired lease does not.
        Assert.True(await store.HasActiveJobAsync("t", now));
        Assert.False(await store.HasActiveJobAsync("t", now + Lease + TimeSpan.FromSeconds(1)));
    }
}
