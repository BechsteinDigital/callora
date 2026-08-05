using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

public sealed class PluginContextTests
{
    [Fact]
    public void PluginConfiguration_ExposesOnlyTheOwningPluginSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["communication:WebRtc:Enabled"] = "true",
                ["BackendHost:JwtSigningKey"] = "must-stay-hidden",
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();
        var context = new PluginContext(
            services,
            "communication",
            (_, _, _) => { },
            _ => null);

        Assert.NotNull(context.PluginConfiguration);
        Assert.Equal("true", context.PluginConfiguration!["WebRtc:Enabled"]);
        Assert.Null(context.PluginConfiguration["BackendHost:JwtSigningKey"]);
    }

    [Fact]
    public async Task SessionResume_IsHandedOutBoundToTheOwningPlugin()
    {
        // Two plugins, one store: the binding is what stops either from redeeming the other's
        // promise, and it has to hold at the point the service is handed out (ADR-018 §2.2).
        var store = new InMemorySessionResumeTicketStore();
        var services = new ServiceCollection()
            .AddSingleton<ISessionResumeTicketStore>(store)
            .BuildServiceProvider();

        var mine = Resume(services, "videoconference");
        var theirs = Resume(services, "communication");
        var ticket = await mine.IssueAsync("conference", "p", TimeSpan.FromMinutes(5));

        Assert.Null(await theirs.RedeemAsync(ticket.Token));
        Assert.NotNull(await mine.RedeemAsync(ticket.Token));
    }

    [Fact]
    public void SessionResume_IsAbsentWithoutAStore()
    {
        // A minimal host without persistence degrades to "no resume" rather than to a broken one.
        var context = new PluginContext(
            new ServiceCollection().BuildServiceProvider(),
            "videoconference",
            (_, _, _) => { },
            _ => null);

        Assert.Null(context.Services.GetService(typeof(IHostSessionResumeService)));
    }

    private static IHostSessionResumeService Resume(IServiceProvider services, string pluginId) =>
        (IHostSessionResumeService)new PluginContext(services, pluginId, (_, _, _) => { }, _ => null)
            .Services
            .GetService(typeof(IHostSessionResumeService))!;
}
