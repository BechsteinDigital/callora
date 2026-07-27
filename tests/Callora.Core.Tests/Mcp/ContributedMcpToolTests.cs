using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Mcp.Contracts;
using Callora.Core.Application.Plugins;
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
/// the handler runs; a granted permission runs the handler and maps its result to text content. The
/// internal availability gate then runs after the permission check and before the handler: an
/// unavailable workspace denies the call without leaking to unauthorized callers.
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

    [Fact]
    public async Task Invoke_WhenPluginUnavailableForWorkspace_IsAnError_AndTheHandlerNeverRuns()
    {
        var handlerRan = false;
        var tool = Build(
            ReadPermission,
            (_, _) =>
            {
                handlerRan = true;
                return Task.FromResult(McpToolResult.Json(new { ok = true }));
            },
            availability: (_, _, _) => Task.FromResult(Unavailable()));
        var user = User(workspaceKey: "ws-token", permissions: ReadPermission);

        var result = await tool.InvokeCoreAsync(user, Args("{}"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.False(handlerRan);
    }

    [Fact]
    public async Task Invoke_WhenPluginAvailableForWorkspace_RunsTheHandler_AndMapsTheResult()
    {
        var tool = Build(
            ReadPermission,
            (_, _) => Task.FromResult(McpToolResult.Json(new { value = 7 })),
            availability: (_, _, _) => Task.FromResult(Available()));
        var user = User(workspaceKey: "ws-token", permissions: ReadPermission);

        var result = await tool.InvokeCoreAsync(user, Args("{}"), CancellationToken.None);

        Assert.False(result.IsError);
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Equal("{\"value\":7}", block.Text);
    }

    [Fact]
    public async Task Invoke_PassesTheContributingPluginIdAndResolvedWorkspace_ToTheAvailabilityGate()
    {
        string? seenPluginId = null;
        string? seenWorkspace = null;
        var tool = Build(
            ReadPermission,
            (_, _) => Task.FromResult(McpToolResult.Json(new { ok = true })),
            availability: (pluginId, workspaceKey, _) =>
            {
                seenPluginId = pluginId;
                seenWorkspace = workspaceKey;
                return Task.FromResult(Available());
            },
            pluginId: "communication.voice");
        var user = User(workspaceKey: "ws-token", permissions: ReadPermission);

        await tool.InvokeCoreAsync(user, Args("{}"), CancellationToken.None);

        Assert.Equal("communication.voice", seenPluginId);
        Assert.Equal("ws-token", seenWorkspace);
    }

    [Fact]
    public async Task Invoke_WithoutPermission_NeverConsultsTheAvailabilityGate()
    {
        var availabilityQueried = false;
        var tool = Build(
            ReadPermission,
            (_, _) => Task.FromResult(McpToolResult.Json(new { ok = true })),
            availability: (_, _, _) =>
            {
                availabilityQueried = true;
                return Task.FromResult(Available());
            });
        // Bound workspace present but the required permission is not granted.
        var user = User(workspaceKey: "ws-token", permissions: "communication.calls.other");

        var result = await tool.InvokeCoreAsync(user, Args("{}"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.False(availabilityQueried);
    }

    private static ContributedMcpTool Build(
        string requiredPermission,
        System.Func<McpToolInvocation, CancellationToken, Task<McpToolResult>> handler,
        System.Func<string, string, CancellationToken, Task<PluginAvailability>>? availability = null,
        string pluginId = "test.plugin")
    {
        var registration = new McpToolRegistration(
            "get_call",
            "Gets a call.",
            FakeMcpToolContributor.EmptySchema(),
            requiredPermission,
            handler);
        // The wrapper resolves the principal itself in InvokeCoreAsync, so the accessor is unused there.
        // When no availability delegate is supplied the wrapper resolves one from the (empty) request
        // services and finds none — fail-open — so the existing auth/scope/handler tests are unaffected.
        return new ContributedMcpTool(registration, pluginId, new HttpContextAccessor(), availability);
    }

    private static PluginAvailability Available() =>
        PluginAvailability.From(new PluginAvailabilityInputs(
            BundledOrInstalled: true,
            RuntimeHealthy: true,
            Entitled: true,
            WorkspaceEnabled: true,
            TenantActive: true,
            WorkspaceActive: true,
            RequiredCapabilitiesAvailable: true));

    private static PluginAvailability Unavailable() =>
        PluginAvailability.From(new PluginAvailabilityInputs(
            BundledOrInstalled: true,
            RuntimeHealthy: true,
            Entitled: false,
            WorkspaceEnabled: true,
            TenantActive: true,
            WorkspaceActive: true,
            RequiredCapabilitiesAvailable: true));

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
