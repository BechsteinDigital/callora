using System.Text.Json;

namespace Callora.Core.Application.Mcp.Contracts;

/// <summary>
/// A single MCP tool a plugin contributes, described in SDK-neutral terms. The host translates each
/// registration into a live transport tool: it advertises <see cref="Name"/>, <see cref="Description"/>
/// and <see cref="InputSchema"/>, enforces <see cref="RequiredPermission"/> plus workspace scope per
/// call, then invokes <see cref="Handler"/> with the resolved workspace. Because plugins name only the
/// permission (not any licensing/entitlement gate), commercialization stays a separate, later axis.
/// </summary>
/// <param name="Name">The tool's unique name as exposed to MCP clients (for example <c>place_call</c>).</param>
/// <param name="Description">A human-readable description shown to MCP clients.</param>
/// <param name="InputSchema">The tool's JSON-Schema input contract as an explicit <see cref="JsonElement"/>.</param>
/// <param name="RequiredPermission">
/// The Callora RBAC permission key the caller must hold (for example <c>communication.calls.read</c>).
/// The host denies the call before the handler runs when it is missing.
/// </param>
/// <param name="Handler">
/// The tool's implementation, invoked with the resolved <see cref="McpToolInvocation"/> once
/// authentication, scope and permission have passed.
/// </param>
public sealed record McpToolRegistration(
    string Name,
    string Description,
    JsonElement InputSchema,
    string RequiredPermission,
    Func<McpToolInvocation, CancellationToken, Task<McpToolResult>> Handler);
