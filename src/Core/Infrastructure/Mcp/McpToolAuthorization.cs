using System.Security.Claims;
using System.Text.Json;
using Callora.Core.Application.Mcp.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;

namespace Callora.Core.Infrastructure.Mcp;

/// <summary>
/// The single authorization truth for MCP tool calls: it resolves the target workspace and checks the
/// tool's required permission against the calling principal. The host authorizes here so plugin
/// handlers receive an already-scoped invocation (mirrors <c>CallAdminScope</c> plus the endpoint
/// permission gate). The token-bound <c>workspace_key</c> claim always wins; a platform operator with
/// no bound workspace must name the target via the <c>workspaceKey</c> tool argument; absent both the
/// call is rejected rather than defaulting to something dangerous. All failures surface as an
/// <see cref="McpToolResult"/> error — never as an exception crossing the transport boundary.
/// </summary>
internal static class McpToolAuthorization
{
    private const string WorkspaceArgumentName = "workspaceKey";

    /// <summary>
    /// Resolves the workspace for a tool call from the principal's bound workspace or the
    /// <c>workspaceKey</c> argument. Returns <see langword="false"/> with an error result when neither
    /// yields a workspace.
    /// </summary>
    public static bool TryResolveWorkspace(
        ClaimsPrincipal user,
        JsonElement arguments,
        out string workspaceKey,
        out McpToolResult? error)
    {
        ArgumentNullException.ThrowIfNull(user);

        var resolved = user.FindFirst(BackendClaimTypes.WorkspaceKey)?.Value;
        if (string.IsNullOrWhiteSpace(resolved))
        {
            resolved = ReadWorkspaceArgument(arguments);
        }

        if (string.IsNullOrWhiteSpace(resolved))
        {
            workspaceKey = string.Empty;
            error = McpToolResult.Error("A workspace is required.");
            return false;
        }

        workspaceKey = resolved.Trim();
        error = null;
        return true;
    }

    /// <summary>Returns whether the principal holds the tool's required permission.</summary>
    public static bool HasPermission(ClaimsPrincipal user, string requiredPermission)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(requiredPermission))
        {
            return true;
        }

        return EndpointAuthorizationExtensions.UserHasPermission(user, requiredPermission);
    }

    private static string? ReadWorkspaceArgument(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty(WorkspaceArgumentName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }
}
