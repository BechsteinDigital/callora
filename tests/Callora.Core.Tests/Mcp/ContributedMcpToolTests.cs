using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Mcp.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Mcp;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Callora.Core.Tests.Mcp;

/// <summary>
/// The contributed MCP tool wrapper (M1): it authorizes each call before running the plugin handler.
/// The token-bound <c>workspace_key</c> claim wins; a platform operator supplies <c>workspaceKey</c> as
/// an argument; absent both the call fails without throwing; a missing required permission denies before
/// the handler runs; a granted permission runs the handler and maps its result to text content.
/// </summary>
public sealed class ContributedMcpToolTests
{
    private const string ReadPermission = "communication.calls.read";

    [Fact]
    public async Task Invoke_WithTokenBoundWorkspaceClaim_PassesThatWorkspaceToTheHandler()
    {
        string? seenWorkspace = null;
        var tool = Build(ReadPermission, (invocation, _) =>
        {
            seenWorkspace = invocation.WorkspaceKey;
            return Task.FromResult(McpToolResult.Json(new { ok = true }));
        });
        var user = User(workspaceKey: "ws-token", permissions: ReadPermission);

        var result = await tool.InvokeCoreAsync(user, Args("{}"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("ws-token", seenWorkspace);
    }

    [Fact]
    public async Task Invoke_AsPlatformOperator_UsesTheWorkspaceKeyArgument()
    {
        string? seenWorkspace = null;
        var tool = Build(ReadPermission, (invocation, _) =>
        {
            seenWorkspace = invocation.WorkspaceKey;
            return Task.FromResult(McpToolResult.Json(new { ok = true }));
        });
        var user = User(workspaceKey: null, permissions: ReadPermission);

        var result = await tool.InvokeCoreAsync(user, Args("{\"workspaceKey\":\"ws-arg\"}"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("ws-arg", seenWorkspace);
    }

    [Fact]
    public async Task Invoke_WithNoWorkspaceAnywhere_IsAnErrorWithoutThrowing()
    {
        var handlerRan = false;
        var tool = Build(ReadPermission, (_, _) =>
        {
            handlerRan = true;
            return Task.FromResult(McpToolResult.Json(new { ok = true }));
        });
        var user = User(workspaceKey: null, permissions: ReadPermission);

        var result = await tool.InvokeCoreAsync(user, Args("{}"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.False(handlerRan);
    }

    [Fact]
    public async Task Invoke_WithoutRequiredPermission_DeniesBeforeTheHandlerRuns()
    {
        var handlerRan = false;
        var tool = Build(ReadPermission, (_, _) =>
        {
            handlerRan = true;
            return Task.FromResult(McpToolResult.Json(new { ok = true }));
        });
        // Bound workspace present but the permission is not granted.
        var user = User(workspaceKey: "ws-token", permissions: "communication.calls.other");

        var result = await tool.InvokeCoreAsync(user, Args("{}"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.False(handlerRan);
    }

    [Fact]
    public async Task Invoke_WithGrantedPermission_RunsTheHandler_AndMapsTheResultToTextContent()
    {
        var tool = Build(ReadPermission, (_, _) =>
            Task.FromResult(McpToolResult.Json(new { value = 42 })));
        var user = User(workspaceKey: "ws-token", permissions: ReadPermission);

        var result = await tool.InvokeCoreAsync(user, Args("{}"), CancellationToken.None);

        Assert.False(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("{\"value\":42}", block.Text);
    }

    private static ContributedMcpTool Build(
        string requiredPermission,
        System.Func<McpToolInvocation, CancellationToken, Task<McpToolResult>> handler)
    {
        var registration = new McpToolRegistration(
            "get_call",
            "Gets a call.",
            FakeMcpToolContributor.EmptySchema(),
            requiredPermission,
            handler);
        // The wrapper resolves the principal itself in InvokeCoreAsync, so the accessor is unused there.
        return new ContributedMcpTool(registration, new HttpContextAccessor());
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static ClaimsPrincipal User(string? workspaceKey, params string[] permissions)
    {
        var claims = new System.Collections.Generic.List<Claim>();
        if (!string.IsNullOrEmpty(workspaceKey))
        {
            claims.Add(new Claim(BackendClaimTypes.WorkspaceKey, workspaceKey));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim(BackendClaimTypes.Permission, permission));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }
}
