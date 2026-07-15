using Callora.Core.Application.Jobs;
using Callora.Core.Infrastructure.Security;

namespace Callora.Administration.Api;

/// <summary>
/// Read-only monitoring endpoints for the background job queue.
/// </summary>
public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs")
            .RequireAuthorization()
            .RequirePermission(BackendPermissionKeys.JobRead);

        group.MapGet("/", async (
            IBackgroundJobStore jobStore,
            BackgroundJobOptions options,
            HttpContext httpContext,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            var effectiveLimit = Math.Clamp(limit ?? options.RecentListLimit, 1, options.RecentListLimit);
            var jobs = await jobStore.ListRecentAsync(effectiveLimit, cancellationToken);

            // Workspace-bound sessions only see their own workspace's jobs.
            if (!WorkspaceScopeEvaluator.IsOperator(httpContext.User))
            {
                var boundWorkspace = httpContext.User
                    .FindFirst(BackendClaimTypes.WorkspaceKey)?.Value?.Trim();
                jobs = jobs
                    .Where(job => string.Equals(job.WorkspaceKey, boundWorkspace, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

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
