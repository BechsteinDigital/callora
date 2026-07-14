using Callora.Host.Backend.Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;

namespace Callora.Host.Backend.Infrastructure.Security;

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
