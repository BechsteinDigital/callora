using Callora.Core.Application.Security;
using Microsoft.AspNetCore.Http;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Derives the request's workspace scope from the authenticated principal.
/// Operators (and any context without an HTTP request, e.g. jobs or seeding)
/// are not workspace-scoped, so the persistence global query filter is
/// bypassed for them (PLAT-267).
/// </summary>
public sealed class HttpWorkspaceScopeContext(IHttpContextAccessor httpContextAccessor) : IWorkspaceScopeContext
{
    public bool IsWorkspaceScoped => Resolve() is not null;

    public string? WorkspaceKey => Resolve();

    public bool IsTenantScoped => ResolveTenant() is not null;

    public string? TenantKey => ResolveTenant();

    private string? ResolveTenant()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null || WorkspaceScopeEvaluator.IsOperator(user))
        {
            return null;
        }

        // Der Scope-Claim wird mitgeprüft und nicht nur der Schlüssel: Ein tenant_key allein sagt
        // "gehört zu diesem Mandanten", nicht "darf mandantenweit lesen". Würde der Filter schon auf
        // den Schlüssel anspringen, öffnete jede spätere Sitzung, die ihren Mandanten der Vollständig-
        // keit halber mitführt, unbeabsichtigt die Sicht auf alle seine Workspaces.
        if (!user.HasClaim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Tenant))
        {
            return null;
        }

        var key = user.FindFirst(BackendClaimTypes.TenantKey)?.Value;
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    private string? Resolve()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null || WorkspaceScopeEvaluator.IsOperator(user))
        {
            return null;
        }

        var key = user.FindFirst(BackendClaimTypes.WorkspaceKey)?.Value;
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }
}
