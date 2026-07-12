using Callora.Host.Backend.Application.Abstractions.Jobs;
using Callora.Host.Backend.Application.Jobs;

namespace Callora.Host.Backend.Api;

/// <summary>
/// Read-only monitoring endpoints for the background job queue.
/// </summary>
public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs")
            .RequireAuthorization();

        group.MapGet("/", async (
            IBackgroundJobStore jobStore,
            BackgroundJobOptions options,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var effectiveLimit = Math.Clamp(limit ?? options.RecentListLimit, 1, options.RecentListLimit);
            var jobs = await jobStore.ListRecentAsync(effectiveLimit, cancellationToken);
            return Results.Ok(jobs.Select(job => new
            {
                job.Id,
                job.JobType,
                Status = job.Status.ToString(),
                job.WorkspaceKey,
                job.AttemptCount,
                job.MaxAttempts,
                job.ScheduledAtUtc,
                job.CreatedAtUtc,
                job.StartedAtUtc,
                job.CompletedAtUtc,
                job.LastError
            }));
        });

        return app;
    }
}
