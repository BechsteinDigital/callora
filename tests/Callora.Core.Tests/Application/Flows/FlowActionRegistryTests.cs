using Callora.Core.Application.Flows;
using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Extensibility;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Application.Flows;

/// <summary>
/// Precedence contract for host+plugin flow-action resolution: plugin-wins,
/// consistent with the job-handler resolver (R1).
/// </summary>
public sealed class FlowActionRegistryTests
{
    [Fact]
    public void Resolve_HostAndPluginHandleSameType_PluginWins()
    {
        var host = new StubHandler("call.accept");
        var plugin = new StubHandler("call.accept");
        var registry = new FlowActionRegistry([host], Catalog(plugin));

        Assert.Same(plugin, registry.Resolve("call.accept"));
    }

    [Fact]
    public void Resolve_HostProtected_HostWinsOverPlugin()
    {
        var host = new ProtectedStubHandler("call.accept");
        var plugin = new StubHandler("call.accept");
        var registry = new FlowActionRegistry([host], Catalog(plugin));

        Assert.Same(host, registry.Resolve("call.accept"));
    }

    [Fact]
    public void Resolve_OnlyHostHandles_ReturnsHost()
    {
        var host = new StubHandler("call.accept");
        var registry = new FlowActionRegistry([host], Catalog());

        Assert.Same(host, registry.Resolve("call.accept"));
    }

    [Fact]
    public void Resolve_TrimsAndIgnoresCase()
    {
        var host = new StubHandler("audio.play");
        var registry = new FlowActionRegistry([host], Catalog());

        Assert.Same(host, registry.Resolve("  AUDIO.PLAY  "));
    }

    [Fact]
    public void Resolve_BlankType_ReturnsNull()
    {
        var registry = new FlowActionRegistry([new StubHandler("call.accept")], Catalog());

        Assert.Null(registry.Resolve("   "));
    }

    private static StaticPluginCatalog Catalog(params IFlowActionHandler[] plugins) =>
        new(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IFlowActionHandler)] = [.. plugins]
        });

    private sealed class StubHandler(string type) : IFlowActionHandler
    {
        public string Type => type;

        public Task ExecuteAsync(
            RuleContext context,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [HostProtected]
    private sealed class ProtectedStubHandler(string type) : IFlowActionHandler
    {
        public string Type => type;

        public Task ExecuteAsync(
            RuleContext context,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
