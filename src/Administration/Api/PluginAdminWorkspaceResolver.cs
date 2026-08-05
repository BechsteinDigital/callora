using Callora.Core.Application.Security;

namespace Callora.Administration.Api;

/// <summary>
/// Resolves the workspace a plugin Admin API request actually operates on (#109).
/// <para>
/// A workspace-bound session always resolves to its own workspace — a
/// <c>?workspaceKey=</c> value can never override it. A platform operator carries
/// no binding and names the target explicitly; that query value is the effective
/// workspace, and the host gates plugin availability against it exactly as it
/// does for a bound one.
/// </para>
/// </summary>
internal static class PluginAdminWorkspaceResolver
{
    internal const string WorkspaceQueryKey = "workspaceKey";

    public static string? Resolve(HttpContext httpContext, string? boundWorkspaceKey)
    {
        if (!string.IsNullOrWhiteSpace(boundWorkspaceKey))
        {
            return boundWorkspaceKey.Trim();
        }

        // Only a platform operator may select a workspace. Any other unbound
        // principal is refused a workspace rather than inheriting the query value.
        if (!WorkspaceScopeEvaluator.IsOperator(httpContext.User))
        {
            return null;
        }

        var requested = httpContext.Request.Query[WorkspaceQueryKey].ToString();
        return string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
    }
}
