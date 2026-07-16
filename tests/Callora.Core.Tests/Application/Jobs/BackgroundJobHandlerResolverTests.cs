using Callora.Core.Application.Jobs;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Application.Jobs;

/// <summary>
/// Precedence contract for host+plugin job-handler resolution: plugin-wins — a
/// plugin handler overrides a host handler of the same job type (R1).
/// </summary>
public sealed class BackgroundJobHandlerResolverTests
{
    [Fact]
    public void Resolve_HostAndPluginHandleSameType_PluginWins()
    {
        var host = new StubHandler("email");
        var plugin = new StubHandler("email");
        var resolver = new BackgroundJobHandlerResolver([host], Catalog(plugin));

        Assert.Same(plugin, resolver.Resolve("email"));
    }

    [Fact]
    public void Resolve_OnlyHostHandles_ReturnsHost_CaseInsensitive()
    {
        var host = new StubHandler("email");
        var resolver = new BackgroundJobHandlerResolver([host], Catalog());

        Assert.Same(host, resolver.Resolve("EMAIL"));
    }

    [Fact]
    public void Resolve_OnlyPluginHandles_ReturnsPlugin()
    {
        var plugin = new StubHandler("sms");
        var resolver = new BackgroundJobHandlerResolver([], Catalog(plugin));

        Assert.Same(plugin, resolver.Resolve("sms"));
    }

    [Fact]
    public void Resolve_NoMatch_ReturnsNull()
    {
        var resolver = new BackgroundJobHandlerResolver([new StubHandler("email")], Catalog());

        Assert.Null(resolver.Resolve("unknown"));
    }

    [Fact]
    public void Resolve_BlankType_ReturnsNull()
    {
        var resolver = new BackgroundJobHandlerResolver([new StubHandler("email")], Catalog());

        Assert.Null(resolver.Resolve("   "));
    }

    private static StaticPluginCatalog Catalog(params IBackgroundJobHandler[] plugins) =>
        new(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IBackgroundJobHandler)] = [.. plugins]
        });

    private sealed class StubHandler(string jobType) : IBackgroundJobHandler
    {
        public string JobType => jobType;

        public Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
