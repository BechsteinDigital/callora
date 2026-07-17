using Callora.Core.Api;
using Callora.Core.Application.Flows;
using Callora.Core.Infrastructure.Security;

namespace Callora.Administration.Api;

/// <summary>
/// Flow automation CRUD: triggers, condition trees and action lists per
/// workspace.
/// </summary>
public static class FlowEndpoints
{
    public static IEndpointRouteBuilder MapFlowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/flows")
            .RequireAuthorization();

        group.MapGet("/", async (
                IFlowStore store,
                string workspaceKey,
                int? limit,
                string? cursor,
                CancellationToken cancellationToken) =>
            {
                var flows = await store.ListAsync(workspaceKey, cancellationToken);
                var ordered = flows
                    .OrderByDescending(static x => x.CreatedAtUtc)
                    .ThenBy(static x => x.Id)
                    .ToArray();
                return Results.Ok(ListPagination.Page(
                    ordered, limit, cursor, static x => x.Id.ToString()));
            })
            .Produces<PagedApiResponse<FlowSnapshot>>()
            .RequirePermission(BackendPermissionKeys.FlowRead)
            .RequireWorkspaceScope();

        group.MapPost("/", async (
                IFlowStore store,
                string workspaceKey,
                UpsertFlowApiRequest request,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TriggerEvent))
                {
                    return ApiProblems.BadRequest("name and triggerEvent are required.");
                }

                var created = await store.UpsertAsync(
                    new FlowSnapshot(
                        Guid.Empty,
                        workspaceKey,
                        request.Name,
                        request.TriggerEvent,
                        request.Conditions?.GetRawText(),
                        request.Actions?.GetRawText() ?? "[]",
                        request.IsActive,
                        request.Priority,
                        DateTimeOffset.UtcNow),
                    cancellationToken);
                return Results.Created($"/api/flows/{created.Id}", created);
            })
            .RequirePermission(BackendPermissionKeys.FlowManage)
            .RequireWorkspaceScope();

        group.MapPut("/{id:guid}", async (
                IFlowStore store,
                Guid id,
                string workspaceKey,
                UpsertFlowApiRequest request,
                CancellationToken cancellationToken) =>
            {
                var existing = await store.GetAsync(id, cancellationToken);
                if (existing is null ||
                    !string.Equals(existing.WorkspaceKey, workspaceKey?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NotFound();
                }

                var updated = await store.UpsertAsync(
                    existing with
                    {
                        Name = request.Name,
                        TriggerEvent = request.TriggerEvent,
                        ConditionsJson = request.Conditions?.GetRawText(),
                        ActionsJson = request.Actions?.GetRawText() ?? existing.ActionsJson,
                        IsActive = request.IsActive,
                        Priority = request.Priority
                    },
                    cancellationToken);
                return Results.Ok(updated);
            })
            .RequirePermission(BackendPermissionKeys.FlowManage)
            .RequireWorkspaceScope();

        group.MapDelete("/{id:guid}", async (
                IFlowStore store,
                Guid id,
                string workspaceKey,
                CancellationToken cancellationToken) =>
            {
                var existing = await store.GetAsync(id, cancellationToken);
                if (existing is null ||
                    !string.Equals(existing.WorkspaceKey, workspaceKey?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NotFound();
                }

                await store.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(BackendPermissionKeys.FlowManage)
            .RequireWorkspaceScope();

        return app;
    }
}
