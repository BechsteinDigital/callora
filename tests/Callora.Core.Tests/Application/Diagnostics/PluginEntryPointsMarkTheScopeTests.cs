using Callora.Core.Application.Diagnostics;
using Callora.Core.Application.Events.Business;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Jobs;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Domain.Jobs;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Application.Diagnostics;

/// <summary>
/// Attribution is only worth as much as its coverage: an entry point that forgets to mark
/// the scope produces work credited to nobody, which reads exactly like host work.
/// </summary>
/// <remarks>
/// These are the same three places that gained the availability gate — they already resolve
/// the owning plugin, so marking the scope costs a lookup nobody has to add.
/// </remarks>
public sealed class PluginEntryPointsMarkTheScopeTests
{
    [Fact]
    public async Task A_background_job_runs_inside_its_plugins_scope()
    {
        var handler = new ScopeObservingJobHandler("plugin.job");
        var catalog = new StaticPluginCatalog(
            new Dictionary<Type, IReadOnlyList<object>> { [typeof(IBackgroundJobHandler)] = [handler] },
            pluginId: "billed-plugin");
        var store = new InMemoryBackgroundJobStore();
        var processor = new BackgroundJobProcessor(
            store,
            new BackgroundJobHandlerResolver([], catalog),
            new BackgroundJobOptions(),
            NullLogger<BackgroundJobProcessor>.Instance);
        await store.AddAsync(BackgroundJob.Create("plugin.job", "{}", DateTimeOffset.UtcNow, 1, "workspace-a", DateTimeOffset.UtcNow));

        await processor.ProcessNextAsync(CancellationToken.None);

        Assert.Equal("billed-plugin", handler.SeenPluginId);
    }

    [Fact]
    public async Task A_host_owned_job_runs_outside_any_plugin_scope()
    {
        // Otherwise host work would be credited to whichever plugin ran before it.
        var handler = new ScopeObservingJobHandler("host.job");
        var store = new InMemoryBackgroundJobStore();
        var processor = new BackgroundJobProcessor(
            store,
            new BackgroundJobHandlerResolver([handler], new StaticPluginCatalog([])),
            new BackgroundJobOptions(),
            NullLogger<BackgroundJobProcessor>.Instance);
        await store.AddAsync(BackgroundJob.Create("host.job", "{}", DateTimeOffset.UtcNow, 1, null, DateTimeOffset.UtcNow));

        await processor.ProcessNextAsync(CancellationToken.None);

        Assert.Null(handler.SeenPluginId);
    }

    [Fact]
    public async Task A_business_event_listener_runs_inside_its_plugins_scope()
    {
        var listener = new ScopeObservingListener();
        var catalog = new StaticPluginCatalog(
            new Dictionary<Type, IReadOnlyList<object>> { [typeof(IBusinessEventListener)] = [listener] },
            pluginId: "billed-plugin");
        var bus = new BusinessEventBus(
            new ServiceCollection().BuildServiceProvider(),
            catalog,
            NullLogger<BusinessEventBus>.Instance);

        await bus.PublishAsync(new ScopeProbeBusinessEvent("workspace-a"));

        Assert.Equal("billed-plugin", listener.SeenPluginId);
    }

    [Fact]
    public async Task A_host_listener_runs_outside_any_plugin_scope()
    {
        var listener = new ScopeObservingListener();
        var services = new ServiceCollection();
        services.AddSingleton<IBusinessEventListener>(listener);
        await using var provider = services.BuildServiceProvider();
        var bus = new BusinessEventBus(provider, new StaticPluginCatalog([]), NullLogger<BusinessEventBus>.Instance);

        await bus.PublishAsync(new ScopeProbeBusinessEvent("workspace-a"));

        Assert.Null(listener.SeenPluginId);
    }
}

internal sealed class ScopeObservingJobHandler(string jobType) : IBackgroundJobHandler
{
    public string JobType { get; } = jobType;

    public string? SeenPluginId { get; private set; }

    public Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        SeenPluginId = PluginExecutionScope.Current;
        return Task.CompletedTask;
    }
}

internal sealed class ScopeObservingListener : IBusinessEventListener
{
    public int Priority => 0;

    public string? SeenPluginId { get; private set; }

    public Task OnBusinessEventAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default)
    {
        SeenPluginId = PluginExecutionScope.Current;
        return Task.CompletedTask;
    }
}

internal sealed class ScopeProbeBusinessEvent(string? workspaceKey) : IBusinessEvent
{
    public string EventName => "thing.happened";

    public string? WorkspaceKey { get; } = workspaceKey;

    public IReadOnlyDictionary<string, string> ToEventData() => new Dictionary<string, string>();
}

/// <summary>
/// The HTTP entry point, kept in its own class because it needs a running test server.
/// </summary>
public sealed class PluginRoutesMarkTheScopeTests
{
    [Fact]
    public async Task A_plugin_route_runs_inside_its_plugins_scope()
    {
        var controller = new ScopeObservingPluginController();
        var catalog = new StaticPluginCatalog(
            new Dictionary<Type, IReadOnlyList<object>>
            {
                [typeof(Callora.Core.Application.Http.Contracts.IApiController)] = [controller]
            },
            pluginId: "billed-plugin");
        var dataSource = new Callora.Core.Infrastructure.Http.PluginApiEndpointDataSource(
            catalog,
            NullLogger<Callora.Core.Infrastructure.Http.PluginApiEndpointDataSource>.Instance);
        dataSource.Refresh();

        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<Callora.Core.Application.Plugins.IPluginAvailabilityEvaluator>(
            new StaticPluginAvailabilityEvaluator());

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources.Add(dataSource);
        await app.StartAsync();

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "test.read");
        var response = await client.GetAsync("/api/scope-probe/who");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("billed-plugin", controller.SeenPluginId);
    }
}

internal sealed class ScopeObservingPluginController : Callora.Core.Application.Http.Contracts.AdminApiController
{
    public string? SeenPluginId { get; private set; }

    [Callora.Core.Application.Http.Contracts.CalloraRoute("GET", "/api/scope-probe/who", Permission = "test.read")]
    public Task<Callora.Core.Application.Http.Contracts.ApiResult> WhoAsync(
        Callora.Core.Application.Http.Contracts.ApiRequest request,
        CancellationToken cancellationToken)
    {
        SeenPluginId = PluginExecutionScope.Current;
        return Task.FromResult(Ok(new { ok = true }));
    }
}
