using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Mcp.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Xunit;

namespace Callora.Core.Tests.Mcp;

/// <summary>
/// The MCP tool registry (M1): registering a plugin adds its tools to the live collection, deregistering
/// removes exactly that plugin's tools, re-registering is idempotent, and two plugins stay isolated so
/// removing one leaves the other's tools intact. It also tracks the contributing plugin's id as
/// provenance and threads it into each tool's availability gate, so a call is gated on the right plugin.
/// </summary>
public sealed class McpToolRegistryTests
{
    [Fact]
    public void Register_AddsAllContributedTools_ToTheCollection()
    {
        var (registry, collection) = NewRegistry();
        var contributor = new FakeMcpToolContributor(
            FakeMcpToolContributor.Tool("alpha"),
            FakeMcpToolContributor.Tool("beta"));

        registry.Register("plugin-a", contributor);

        Assert.Equal(2, collection.Count);
        Assert.Contains("alpha", collection.PrimitiveNames);
        Assert.Contains("beta", collection.PrimitiveNames);
    }

    [Fact]
    public void Deregister_RemovesThePluginTools_LeavingTheCollectionEmpty()
    {
        var (registry, collection) = NewRegistry();
        registry.Register("plugin-a", new FakeMcpToolContributor(
            FakeMcpToolContributor.Tool("alpha"),
            FakeMcpToolContributor.Tool("beta")));

        registry.Deregister("plugin-a");

        Assert.Empty(collection);
    }

    [Fact]
    public void Register_IsIdempotent_WhenTheSamePluginRegistersTwice()
    {
        var (registry, collection) = NewRegistry();
        var contributor = new FakeMcpToolContributor(
            FakeMcpToolContributor.Tool("alpha"),
            FakeMcpToolContributor.Tool("beta"));

        registry.Register("plugin-a", contributor);
        registry.Register("plugin-a", contributor);

        Assert.Equal(2, collection.Count);
    }

    [Fact]
    public void Deregister_OfOnePlugin_LeavesAnotherPluginsToolsIntact()
    {
        var (registry, collection) = NewRegistry();
        registry.Register("plugin-a", new FakeMcpToolContributor(FakeMcpToolContributor.Tool("alpha")));
        registry.Register("plugin-b", new FakeMcpToolContributor(FakeMcpToolContributor.Tool("beta")));

        registry.Deregister("plugin-a");

        Assert.Single(collection);
        Assert.Contains("beta", collection.PrimitiveNames);
        Assert.DoesNotContain("alpha", collection.PrimitiveNames);
    }

    [Fact]
    public void Deregister_OfUnknownPlugin_IsANoOp()
    {
        var (registry, collection) = NewRegistry();
        registry.Register("plugin-a", new FakeMcpToolContributor(FakeMcpToolContributor.Tool("alpha")));

        registry.Deregister("plugin-unknown");

        Assert.Single(collection);
    }

    [Fact]
    public async Task Register_ThreadsTheContributingPluginId_ThroughToTheAvailabilityGate()
    {
        // Availability keyed by plugin id: plugin-a is available, plugin-b is not. The registry must gate
        // each tool on the plugin that contributed it — proving the Register(pluginId, …) provenance is
        // what reaches the evaluator, not something ambient.
        var evaluator = new RecordingAvailabilityEvaluator(availableByPluginId: new()
        {
            ["plugin-a"] = true,
            ["plugin-b"] = false
        });
        var accessor = AccessorWith(evaluator);
        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        var registry = new McpToolRegistry(collection, accessor);

        var aRan = false;
        var bRan = false;
        registry.Register("plugin-a", new FakeMcpToolContributor(Handled("tool-a", () => aRan = true)));
        registry.Register("plugin-b", new FakeMcpToolContributor(Handled("tool-b", () => bRan = true)));

        var user = User("ws-token");
        var resultA = await Invoke(collection, "tool-a", user);
        var resultB = await Invoke(collection, "tool-b", user);

        // Only the available plugin's tool runs; the unavailable one is denied without invoking its handler.
        Assert.False(resultA.IsError);
        Assert.True(aRan);
        Assert.True(resultB.IsError);
        Assert.False(bRan);

        // The gate was consulted with each contributing plugin's own id.
        Assert.Contains("plugin-a", evaluator.SeenPluginIds);
        Assert.Contains("plugin-b", evaluator.SeenPluginIds);
    }

    private static McpToolRegistration Handled(string name, System.Action onRun) =>
        FakeMcpToolContributor.Tool(name, handler: (_, _) =>
        {
            onRun();
            return Task.FromResult(McpToolResult.Json(new { ok = true }));
        });

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> Invoke(
        McpServerPrimitiveCollection<McpServerTool> collection,
        string toolName,
        ClaimsPrincipal user)
    {
        Assert.True(collection.TryGetPrimitive(toolName, out var tool));
        var contributed = Assert.IsType<ContributedMcpTool>(tool);
        var args = JsonDocument.Parse("{}").RootElement;
        return await contributed.InvokeCoreAsync(user, args, CancellationToken.None);
    }

    private static ClaimsPrincipal User(string workspaceKey)
    {
        var claims = new List<Claim>
        {
            new(BackendClaimTypes.WorkspaceKey, workspaceKey)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private static IHttpContextAccessor AccessorWith(IPluginAvailabilityEvaluator evaluator)
    {
        var services = new ServiceCollection();
        services.AddSingleton(evaluator);
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        return new HttpContextAccessor { HttpContext = context };
    }

    private static (McpToolRegistry Registry, McpServerPrimitiveCollection<McpServerTool> Collection) NewRegistry()
    {
        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        var registry = new McpToolRegistry(collection, new HttpContextAccessor());
        return (registry, collection);
    }

    // Records the plugin ids it is asked about and answers availability from a per-plugin map.
    private sealed class RecordingAvailabilityEvaluator(Dictionary<string, bool> availableByPluginId)
        : IPluginAvailabilityEvaluator
    {
        public ConcurrentBag<string> SeenPluginIds { get; } = new();

        public Task<PluginAvailability> EvaluateAsync(
            string pluginId,
            string workspaceKey,
            CancellationToken cancellationToken = default)
        {
            SeenPluginIds.Add(pluginId);
            var available = availableByPluginId.TryGetValue(pluginId, out var value) && value;
            return Task.FromResult(PluginAvailability.From(new PluginAvailabilityInputs(
                BundledOrInstalled: true,
                RuntimeHealthy: true,
                Entitled: available,
                WorkspaceEnabled: true,
                TenantActive: true,
                WorkspaceActive: true,
                RequiredCapabilitiesAvailable: true)));
        }
    }
}
