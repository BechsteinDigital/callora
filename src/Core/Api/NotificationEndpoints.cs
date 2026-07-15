using Callora.Core.Application.Notifications;
using Callora.Core.Infrastructure.Security;

namespace Callora.Core.Api;

/// <summary>
/// In-app notification center endpoints.
/// </summary>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications")
            .RequireAuthorization();

        group.MapGet("/", async (
                INotificationStore store,
                string? workspaceKey,
                bool? includeRead,
                int? limit,
                CancellationToken cancellationToken) =>
                Results.Ok(await store.ListAsync(
                    workspaceKey,
                    includeRead ?? false,
                    limit ?? 50,
                    cancellationToken)))
            .RequirePermission(BackendPermissionKeys.NotificationRead)
            .RequireWorkspaceScope();

        group.MapPut("/{id:guid}/read", async (
                INotificationStore store,
                Guid id,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var existing = await store.GetAsync(id, cancellationToken);
                if (existing is null)
                    return Results.NotFound();
                // Global notifications (no workspace) stay operator-only for
                // mutation; workspace-bound users only mark their own.
                if (!WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, existing.WorkspaceKey))
                    return Results.Forbid();

                await store.MarkReadAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(BackendPermissionKeys.NotificationRead);

        return app;
    }
}
