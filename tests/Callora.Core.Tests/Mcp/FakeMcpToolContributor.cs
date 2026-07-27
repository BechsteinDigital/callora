using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Mcp.Contracts;

namespace Callora.Core.Tests.Mcp;

/// <summary>A test contributor exposing a fixed set of MCP tool registrations.</summary>
internal sealed class FakeMcpToolContributor : IMcpToolContributor
{
    public FakeMcpToolContributor(params McpToolRegistration[] tools) => Tools = tools;

    public IReadOnlyList<McpToolRegistration> Tools { get; }

    public static McpToolRegistration Tool(
        string name,
        string requiredPermission = "",
        Func<McpToolInvocation, CancellationToken, Task<McpToolResult>>? handler = null) =>
        new(
            name,
            $"{name} description",
            EmptySchema(),
            requiredPermission,
            handler ?? ((_, _) => Task.FromResult(McpToolResult.Json(new { ok = true }))));

    public static JsonElement EmptySchema() =>
        JsonDocument.Parse("{\"type\":\"object\"}").RootElement;
}
