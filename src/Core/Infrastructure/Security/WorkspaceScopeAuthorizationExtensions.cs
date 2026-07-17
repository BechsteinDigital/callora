namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Endpoint-level enforcement of the workspace scope carried by workspace
/// login tokens. The requested workspace is read from the "workspaceKey"
/// query parameter or route value.
/// </summary>
public static class WorkspaceScopeAuthorizationExtensions
{
    private const string WorkspaceKeyParameterName = "workspaceKey";

    /// <summary>
    /// Rejects requests whose workspaceKey does not match the workspace bound
    /// to the caller's token. Only platform-scoped sessions and admins pass
    /// unconstrained; principals without scope or binding are rejected.
    /// </summary>
    public static TBuilder RequireWorkspaceScope<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.RequireAuthorization(policy =>
            policy.RequireAssertion(context =>
            {
                if (context.Resource is not HttpContext httpContext)
                {
                    return false;
                }

                return WorkspaceScopeEvaluator.HasWorkspaceAccess(
                    context.User,
                    ResolveRequestedWorkspaceKey(httpContext));
            }));

        return builder;
    }

    private static string? ResolveRequestedWorkspaceKey(HttpContext httpContext)
    {
        if (httpContext.Request.Query.TryGetValue(WorkspaceKeyParameterName, out var queryValue))
        {
            return queryValue.ToString();
        }

        return httpContext.Request.RouteValues.TryGetValue(WorkspaceKeyParameterName, out var routeValue)
            ? routeValue?.ToString()
            : null;
    }
}
