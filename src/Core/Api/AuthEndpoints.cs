using Callora.Core.Domain.Security;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Microsoft.AspNetCore.Mvc;
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
            // Ausdrücklich aus den Diensten und ausdrücklich nullbar. Ohne [FromServices] rät eine
            // Minimal-API „Body" für jeden Typ, den der Container beim Aufbau der Route nicht kennt —
            // in einem Testhost, der nur die Hälfte registriert, verlangte die Anmeldung dann einen
            // zweiten Rumpf.
            //
            // Und ohne diesen Parameter überhaupt bekam der Helfer seinen Default null: Der Satz aus
            // Plugin-Schlüsseln und zugewiesenen Rollen entstand dann nie, jede Anmeldung fiel still
            // auf den festen Kern-Satz zurück. Getestet war die Zusammensetzung — nur nicht der Weg
            // dorthin, und der ist es, der im Betrieb zählt.
            [FromServices] WorkspaceSessionPermissions? sessionPermissions,
            CancellationToken cancellationToken) =>
            HandleAdminLoginAsync(
                request.Login,
                request.Password,
                request.WorkspaceKey,
                request.TenantKey,
                options,
                userStore,
                rbacStore,
                httpContext,
                cancellationToken,
                sessionPermissions))
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

        // Der Bereichswechsel. Er ist keine Client-Umschaltung: Der Bereich steht im Token
        // (callora_scope), also braucht ein Wechsel eine neue Sitzung — deshalb stellt der Server
        // eine aus, statt das Kennwort ein zweites Mal zu verlangen.
        //
        // Eskalation ist ausgeschlossen, ohne dass hier etwas dagegen steht: Die Auflösung ist
        // dieselbe wie beim Anmelden, und die vergibt Plattform-Scope nur an eine Betreiber-Rolle,
        // Workspace nur an ein Mitglied, Mandant nur an eine Mandanten-Mitgliedschaft. Wer hier
        // etwas bekommt, hätte es auch über /login bekommen — nur eben mit Kennwort.
        protectedApiGroup.MapPost("/scope", async (
            SwitchScopeApiRequest request,
            BackendHostOptions options,
            IBackendUserStore userStore,
            IBackendRbacStore rbacStore,
            HttpContext httpContext,
            [FromServices] WorkspaceSessionPermissions? sessionPermissions,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.FindFirst("sub")?.Value ??
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            // Frisch geladen, nicht aus den Claims rekonstruiert: Der Sicherheitsstempel der neuen
            // Sitzung muss der aktuelle sein. Sonst verlängerte ein Wechsel eine Sitzung über eine
            // Deaktivierung oder Kennwortänderung hinweg, die sie gerade widerrufen hat (#105).
            var user = await userStore
                .GetByExternalIdAsync(userId, cancellationToken)
                .ConfigureAwait(false);
            if (user is null)
            {
                return Results.Forbid();
            }

            return await IssueSessionAsync(
                    user, request.WorkspaceKey, request.TenantKey, options, userStore, rbacStore,
                    httpContext, cancellationToken, sessionPermissions)
                .ConfigureAwait(false);
        }).WithName("Auth_Api_SwitchScope")
            .RequireSameOriginLogin()
            .RequireRateLimiting(BackendRateLimiting.AuthPolicy);

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
            [FromServices] WorkspaceSessionPermissions? sessionPermissions,
            CancellationToken cancellationToken) =>
            HandleAdminLoginAsync(
                request.Login,
                request.Password,
                request.WorkspaceKey,
                // Kein Mandantenschlüssel: Diese Route ist die Anmeldung AN einem Workspace. Die
                // Mandantenebene verwaltet, sie arbeitet nicht — sie geht über /api/login.
                tenantKey: null,
                options,
                userStore,
                rbacStore,
                httpContext,
                cancellationToken,
                sessionPermissions))
            .WithName("Auth_Workspace_Login")
            .RequireSameOriginLogin()
            .RequireRateLimiting(BackendRateLimiting.AuthPolicy);
    }

    private static async Task<IResult> HandleAdminLoginAsync(
        string login,
        string password,
        string? workspaceKey,
        string? tenantKey,
        BackendHostOptions options,
        IBackendUserStore userStore,
        IBackendRbacStore rbacStore,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        // Optional, weil ein Aufbau ohne Plugin-Persistenz weiterhin anmelden können muss — dann
        // eben mit dem festen Kern-Satz der Mitgliedsrolle. Gebunden wird der Dienst an der Route;
        // hier stünde [FromServices] wirkungslos.
        WorkspaceSessionPermissions? sessionPermissions = null)
    {
        var user = await userStore.AuthenticateAsync(login, password, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        return await IssueSessionAsync(
                user, workspaceKey, tenantKey, options, userStore, rbacStore, httpContext,
                cancellationToken, sessionPermissions)
            .ConfigureAwait(false);
    }

    // Alles ab der bestätigten Identität: auflösen, prüfen, Token ausstellen, Cookie setzen. Geteilt
    // mit dem Bereichswechsel, weil dieser sich vom Anmelden nur darin unterscheidet, WOHER die
    // Identität kommt — und weil ein zweiter Ausstellungspfad genau der Ort wäre, an dem eine der
    // beiden Prüfungen später fehlt.
    private static async Task<IResult> IssueSessionAsync(
        BackendUser user,
        string? workspaceKey,
        string? tenantKey,
        BackendHostOptions options,
        IBackendUserStore userStore,
        IBackendRbacStore rbacStore,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        WorkspaceSessionPermissions? sessionPermissions)
    {
        var grant = await AdminLoginResolver
            .ResolveAsync(
                user, workspaceKey, userStore, rbacStore, options, cancellationToken, sessionPermissions, tenantKey)
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

        if (!string.IsNullOrWhiteSpace(grant.TenantKey))
        {
            customClaims[BackendClaimTypes.TenantKey] = grant.TenantKey;
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
            WorkspaceKey: grant.WorkspaceKey,
            TenantKey: grant.TenantKey));
    }
}
