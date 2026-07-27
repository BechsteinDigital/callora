using Callora.Core.Infrastructure.Mcp;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using Xunit;

namespace Callora.Core.Tests.Mcp;

/// <summary>
/// The MCP tool registry (M1): registering a plugin adds its tools to the live collection, deregistering
/// removes exactly that plugin's tools, re-registering is idempotent, and two plugins stay isolated so
/// removing one leaves the other's tools intact.
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

    private static (McpToolRegistry Registry, McpServerPrimitiveCollection<McpServerTool> Collection) NewRegistry()
    {
        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        var registry = new McpToolRegistry(collection, new HttpContextAccessor());
        return (registry, collection);
    }
}
