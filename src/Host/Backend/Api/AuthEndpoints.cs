using Callora.Host.Backend.Application.Abstractions.Security;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Security;
using Callora.Host.Workspace.Api;
using System.Security.Claims;

namespace Callora.Host.Backend.Api;

public static class AuthEndpoints
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var apiGroup = endpoints.MapGroup("/api/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        apiGroup.MapPost("/login", async (
            LoginApiRequest request,
            BackendHostOptions options,
            IBackendUserStore userStore,
            IBackendRbacStore rbacStore,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var user = await userStore.AuthenticateAsync(request.Login, request.Password, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var role = await rbacStore.GetUserRoleAsync(user.ExternalId, cancellationToken).ConfigureAwait(false);
            var roles = string.IsNullOrWhiteSpace(role) ? Array.Empty<string>() : [role];
            var token = BackendJwtTokenIssuer.Issue(
                options,
                subject: user.ExternalId,
                displayName: user.DisplayName,
                email: user.Email,
                roles: roles,
                lifetime: AccessTokenLifetime);

            BackendAuthCookieService.AppendAuthCookie(
                httpContext.Response,
                options,
                token,
                AccessTokenLifetime,
                httpContext.Request.IsHttps);

            return Results.Ok(new LoginApiResponse(
                AccessToken: token,
                TokenType: "Bearer",
                ExpiresInSeconds: (int)AccessTokenLifetime.TotalSeconds,
                UserId: user.ExternalId,
                DisplayName: user.DisplayName,
                Email: user.Email,
                Role: role,
                WorkspaceKey: null));
        }).WithName("Auth_Api_Login")
            .RequireRateLimiting(BackendRateLimiting.AuthPolicy);

        apiGroup.MapPost("/logout", (
            BackendHostOptions options,
            HttpContext httpContext) =>
        {
            BackendAuthCookieService.ClearAuthCookie(
                httpContext.Response,
                options,
                httpContext.Request.IsHttps);
            return Results.NoContent();
        }).WithName("Auth_Api_Logout")
            .RequireRateLimiting(BackendRateLimiting.AuthPolicy);

        var protectedApiGroup = endpoints.MapGroup("/api/auth")
            .WithTags("Auth")
            .RequireAuthorization();

        protectedApiGroup.MapGet("/me", (HttpContext httpContext) =>
        {
            var user = httpContext.User;
            var userId = user.FindFirst("sub")?.Value ??
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new AuthMeApiResponse(
                UserId: userId,
                DisplayName: user.FindFirst(ClaimTypes.Name)?.Value,
                Email: user.FindFirst(ClaimTypes.Email)?.Value,
                Role: user.FindFirst(ClaimTypes.Role)?.Value));
        }).WithName("Auth_Api_Me");

        var workspaceGroup = endpoints.MapGroup("/workspace/auth")
            .WithTags("Workspace Auth")
            .AllowAnonymous();

        workspaceGroup.MapPost("/login", async (
            WorkspaceLoginApiRequest request,
            BackendHostOptions options,
            IBackendUserStore userStore,
            IBackendRbacStore rbacStore,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var user = await userStore.AuthenticateAsync(request.Login, request.Password, cancellationToken).ConfigureAwait(false);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var isMember = await userStore
                .IsWorkspaceMemberAsync(user.ExternalId, request.WorkspaceKey, cancellationToken)
                .ConfigureAwait(false);
            if (!isMember)
            {
                return Results.Forbid();
            }

            var role = await rbacStore.GetUserRoleAsync(user.ExternalId, cancellationToken).ConfigureAwait(false);
            var roles = string.IsNullOrWhiteSpace(role) ? Array.Empty<string>() : [role];
            var token = BackendJwtTokenIssuer.Issue(
                options,
                subject: user.ExternalId,
                displayName: user.DisplayName,
                email: user.Email,
                roles: roles,
                customClaims: new Dictionary<string, string>
                {
                    [BackendClaimTypes.WorkspaceKey] = request.WorkspaceKey.Trim()
                },
                lifetime: AccessTokenLifetime);

            BackendAuthCookieService.AppendAuthCookie(
                httpContext.Response,
                options,
                token,
                AccessTokenLifetime,
                httpContext.Request.IsHttps);

            return Results.Ok(new LoginApiResponse(
                AccessToken: token,
                TokenType: "Bearer",
                ExpiresInSeconds: (int)AccessTokenLifetime.TotalSeconds,
                UserId: user.ExternalId,
                DisplayName: user.DisplayName,
                Email: user.Email,
                Role: role,
                WorkspaceKey: request.WorkspaceKey.Trim()));
        }).WithName("Auth_Workspace_Login")
            .RequireRateLimiting(BackendRateLimiting.AuthPolicy);
    }
}
