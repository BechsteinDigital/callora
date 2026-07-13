using Callora.Host.Backend.Application.Abstractions.Security;
using Callora.Host.Backend.Domain.Security;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Backend.Api;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/", async (
            IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            var users = await userStore.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(users.Select(ToResponse).ToArray());
        }).WithName("Users_List")
            .RequirePermission(BackendPermissionKeys.UserRead);

        group.MapGet("/{userId}", async (
            string userId,
            IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            var user = await userStore.GetByExternalIdAsync(userId, cancellationToken).ConfigureAwait(false);
            return user is null ? Results.NotFound() : Results.Ok(ToResponse(user));
        }).WithName("Users_Get")
            .RequirePermission(BackendPermissionKeys.UserRead);

        group.MapPost("/", async (
            CreateBackendUserApiRequest request,
            IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var user = await userStore.UpsertCredentialsAsync(
                        request.ExternalId,
                        request.Email,
                        request.DisplayName,
                        request.Password,
                        cancellationToken)
                    .ConfigureAwait(false);
                return Results.Created($"/api/users/{user.ExternalId}", ToResponse(user));
            }
            catch (InvalidOperationException ex)
            {
                return ApiProblems.BadRequest(ex.Message);
            }
        }).WithName("Users_Create")
            .RequirePermission(BackendPermissionKeys.UserCreate);

        group.MapPut("/{userId}", async (
            string userId,
            UpdateBackendUserApiRequest request,
            IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var user = await userStore.UpsertCredentialsAsync(
                        userId,
                        request.Email,
                        request.DisplayName,
                        request.Password,
                        cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(ToResponse(user));
            }
            catch (InvalidOperationException ex)
            {
                return ApiProblems.BadRequest(ex.Message);
            }
        }).WithName("Users_Update")
            .RequirePermission(BackendPermissionKeys.UserUpdate);

        group.MapDelete("/{userId}", async (
            string userId,
            IBackendUserStore userStore,
            CancellationToken cancellationToken) =>
        {
            var removed = await userStore.RemoveAsync(userId, cancellationToken).ConfigureAwait(false);
            return removed ? Results.NoContent() : Results.NotFound();
        }).WithName("Users_Delete")
            .RequirePermission(BackendPermissionKeys.UserDelete);
    }

    private static BackendUserApiResponse ToResponse(BackendUser user)
    {
        return new BackendUserApiResponse(
            ExternalId: user.ExternalId,
            Email: user.Email,
            DisplayName: user.DisplayName,
            HasPassword: !string.IsNullOrWhiteSpace(user.PasswordHash),
            PasswordHashAlgorithm: user.PasswordHashAlgorithm,
            CreatedAtUtc: user.CreatedAtUtc,
            UpdatedAtUtc: user.UpdatedAtUtc);
    }
}
