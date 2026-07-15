using Callora.Core.Api;
using System.Text.Json;
using Callora.Core.Application.Entitlements;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Administration.Api;

/// <summary>
/// Inbound marketplace entitlement sync. Events are validated and handed to
/// the durable job queue; the host contains no billing logic (PLAT-102).
/// </summary>
public static class EntitlementSyncEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapEntitlementSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/entitlements")
            .RequireAuthorization()
            // Entitlement grants/revokes are an operator/marketplace concern —
            // never reachable with a plain authenticated session.
            .RequirePermission(BackendPermissionKeys.PluginExecute);

        group.MapPost("/sync", async (
            MarketplaceEntitlementEventPayload payload,
            IBackgroundJobQueue jobQueue,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(payload.EventId) ||
                string.IsNullOrWhiteSpace(payload.PluginId) ||
                string.IsNullOrWhiteSpace(payload.TenantKey))
            {
                return ApiProblems.BadRequest("eventId, pluginId and tenantKey are required.");
            }

            if (!MarketplaceEntitlementActions.IsSupported(payload.Action))
            {
                return ApiProblems.BadRequest($"Unsupported action '{payload.Action}'. Supported: grant, revoke.");
            }

            var jobId = await jobQueue.EnqueueAsync(
                new BackgroundJobRequest(
                    JobType: MarketplaceEntitlementSyncJobHandler.JobTypeName,
                    PayloadJson: JsonSerializer.Serialize(payload, JsonOptions),
                    MaxAttempts: 5,
                    WorkspaceKey: payload.WorkspaceKey),
                cancellationToken);

            return Results.Accepted($"/api/jobs", new { jobId });
        });

        return app;
    }
}
