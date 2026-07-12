using Callora.Host.Backend.Application.Abstractions.Notifications;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Backend.Api;

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
                CancellationToken cancellationToken) =>
                await store.MarkReadAsync(id, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound())
            .RequirePermission(BackendPermissionKeys.NotificationRead);

        return app;
    }
}
