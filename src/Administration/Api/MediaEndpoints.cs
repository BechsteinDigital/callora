using Callora.Core.Api;
using Callora.Core.Application.Media;
using Callora.Core.Infrastructure.Security;

namespace Callora.Administration.Api;

/// <summary>
/// Workspace media assets: upload, list, stream and delete. Bytes are
/// addressed by id only — the storage never sees client-supplied paths.
/// </summary>
public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media")
            .RequireAuthorization();

        group.MapGet("/", async (
                IMediaStore store,
                string workspaceKey,
                string? folder,
                int? limit,
                string? cursor,
                CancellationToken cancellationToken) =>
            {
                var items = await store.ListAsync(workspaceKey, folder, cancellationToken);
                var ordered = items
                    .OrderByDescending(static x => x.CreatedAtUtc)
                    .ThenBy(static x => x.Id)
                    .ToArray();
                return Results.Ok(ListPagination.Page(
                    ordered, limit, cursor, static x => x.Id.ToString()));
            })
            .Produces<PagedApiResponse<MediaItemSnapshot>>()
            .RequirePermission(BackendPermissionKeys.MediaRead)
            .RequireWorkspaceScope();

        group.MapPost("/", async (
                IMediaStore store,
                IMediaStorage storage,
                HttpContext httpContext,
                string workspaceKey,
                string? folder,
                IFormFile file,
                CancellationToken cancellationToken) =>
            {
                if (!MediaUploadPolicy.IsAllowedContentType(file.ContentType))
                {
                    return ApiProblems.BadRequest($"Content type '{file.ContentType}' is not allowed.");
                }

                if (!MediaUploadPolicy.IsAllowedSize(file.Length))
                {
                    return ApiProblems.BadRequest($"File size must be between 1 byte and {MediaUploadPolicy.MaxSizeBytes} bytes.");
                }

                var item = await store.AddAsync(
                    workspaceKey,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    folder ?? "general",
                    httpContext.User.Identity?.Name,
                    cancellationToken);

                await using var content = file.OpenReadStream();
                await storage.WriteAsync(item.Id, content, cancellationToken);
                return Results.Created($"/api/media/{item.Id}", item);
            })
            .RequirePermission(BackendPermissionKeys.MediaManage)
            .RequireWorkspaceScope()
            .DisableAntiforgery();

        group.MapGet("/{id:guid}/content", async (
                IMediaStore store,
                IMediaStorage storage,
                Guid id,
                string workspaceKey,
                CancellationToken cancellationToken) =>
            {
                var item = await store.GetAsync(id, cancellationToken);
                if (item is null ||
                    !string.Equals(item.WorkspaceKey, workspaceKey?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NotFound();
                }

                var stream = await storage.OpenReadAsync(id, cancellationToken);
                return stream is null
                    ? Results.NotFound()
                    : Results.Stream(stream, item.ContentType, item.FileName);
            })
            .RequirePermission(BackendPermissionKeys.MediaRead)
            .RequireWorkspaceScope();

        group.MapDelete("/{id:guid}", async (
                IMediaStore store,
                IMediaStorage storage,
                Guid id,
                string workspaceKey,
                CancellationToken cancellationToken) =>
            {
                var item = await store.GetAsync(id, cancellationToken);
                if (item is null ||
                    !string.Equals(item.WorkspaceKey, workspaceKey?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NotFound();
                }

                await store.DeleteAsync(id, cancellationToken);
                await storage.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(BackendPermissionKeys.MediaManage)
            .RequireWorkspaceScope();

        return app;
    }
}
