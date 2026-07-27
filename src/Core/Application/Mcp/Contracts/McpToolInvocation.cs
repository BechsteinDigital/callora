using System.Text.Json;

namespace Callora.Core.Application.Mcp.Contracts;

/// <summary>
/// The context handed to an <see cref="IMcpToolContributor"/> tool handler after the host has
/// authenticated the caller, resolved the target workspace and checked the tool's required permission.
/// The handler receives an <em>already-scoped</em> invocation and never re-derives the workspace: a
/// single authorization truth lives in the host (mirrors the call-control admin scope helper).
/// </summary>
/// <param name="Arguments">The tool's raw JSON arguments as received from the MCP client.</param>
/// <param name="WorkspaceKey">The resolved workspace the invocation operates on; never empty.</param>
public sealed record McpToolInvocation(JsonElement Arguments, string WorkspaceKey);
