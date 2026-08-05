using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using System.Security.Claims;

namespace Callora.Core.Api;

public static class AuthEndpoints
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var apiGroup = endpoints.MapGroup("/api/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        // The shared admin login (ADR-014 §3.3): every administrative role signs in
        // here. Platform operators omit the workspace key and get a platform-scoped
        // session; workspace admins name their workspace and get a workspace-scoped
        // one. The scope decision lives in AdminLoginResolver.
        apiGroup.MapPost("/login", (
            LoginApiRequest request,
            BackendHostOptions options,
            IBackendUserStore userStore,
            IBackendRbacStore rbacStore,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
            HandleAdminLoginAsync(
                request.Login,
                request.Password,
                request.WorkspaceKey,
                options,
                userStore,
                rbacStore,
                httpContext,
                cancellationToken))
            .WithName("Auth_Api_Login")
            .RequireSameOriginLogin()
            .RequireRateLimiting(BackendRateLimiting.AuthPolicy);

        // Logout is anonymous by design (an expired cookie must still be clearable),
        // but it revokes server-side whatever valid session it was given (#105) —
        // clearing the browser cookie alone would leave a copied token usable.
        apiGroup.MapPost("/logout", async (
            BackendHostOptions options,
            HttpContext httpContext,
            IBackendSessionRevocationStore revocationStore,
            CancellationToken cancellationToken) =>
        {
            await BackendSessionRevocation
                .RevokeCurrentSessionAsync(httpContext, revocationStore, cancellationToken)
                .ConfigureAwait(false);

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

        // Deprecated alias — the workspace-admin login is now the shared admin
        // login above (ADR-014 §14). Retained so the existing workspace shell keeps
        // working until its calls are migrated during the admin-shell rebuild (#30).
        // New clients POST /api/auth/login with an optional workspaceKey.
        workspaceGroup.MapPost("/login", (
            WorkspaceLoginApiRequest request,
            BackendHostOptions options,
            IBackendUserStore userStore,
            IBackendRbacStore rbacStore,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
            HandleAdminLoginAsync(
                request.Login,
                request.Password,
                request.WorkspaceKey,
                options,
                userStore,
                rbacStore,
                httpContext,
                cancellationToken))
            .WithName("Auth_Workspace_Login")
            .RequireSameOriginLogin()
            .RequireRateLimiting(BackendRateLimiting.AuthPolicy);
    }

    private static async Task<IResult> HandleAdminLoginAsync(
        string login,
        string password,
        string? workspaceKey,
        BackendHostOptions options,
        IBackendUserStore userStore,
        IBackendRbacStore rbacStore,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await userStore.AuthenticateAsync(login, password, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var grant = await AdminLoginResolver
            .ResolveAsync(user, workspaceKey, userStore, rbacStore, options, cancellationToken)
            .ConfigureAwait(false);
        if (grant is null)
        {
            return Results.Forbid();
        }

        // Operator sessions may be restricted to the external identity provider, so
        // the second factor lives where one exists (#104). Workspace logins are
        // unaffected.
        if (options.RequireExternalIdentityForOperators &&
            string.Equals(grant.Scope, BackendAuthScopes.Platform, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }

        var roles = string.IsNullOrWhiteSpace(grant.Role) ? Array.Empty<string>() : [grant.Role];
        var customClaims = new Dictionary<string, string>
        {
            [BackendClaimTypes.CalloraScope] = grant.Scope,
            // Binds the session to the account state it was issued under: rotating the
            // stamp (password change, deactivation, RBAC change) revokes it (#105).
            [BackendClaimTypes.SecurityStamp] = user.SecurityStamp
        };
        if (!string.IsNullOrWhiteSpace(grant.WorkspaceKey))
        {
            customClaims[BackendClaimTypes.WorkspaceKey] = grant.WorkspaceKey;
        }

        var token = BackendJwtTokenIssuer.Issue(
            options,
            subject: user.ExternalId,
            displayName: user.DisplayName,
            email: user.Email,
            roles: roles,
            customClaims: customClaims,
            lifetime: AccessTokenLifetime,
            permissions: grant.Permissions);

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
            Role: grant.Role,
            WorkspaceKey: grant.WorkspaceKey));
    }
}
