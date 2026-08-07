using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Surfaces.Contracts;
using Callora.Core.Tests.Support;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
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
    public void SurfaceContextBroadcaster_IsReachableFromAPlugin()
    {
        // Es ist als Extension-Point markiert („resolve to publish surface context from server-side
        // events") — und genau das konnte niemand: Der kuratierte Provider gibt nur Contracts-
        // Namespaces heraus, und der Broadcaster lag daneben. Ein Plugin, das einen Kontextwert
        // veröffentlichen will, bekam null und hätte es nur an einem leeren Panel gemerkt.
        var broadcaster = new SurfaceContextBroadcaster();
        var services = new ServiceCollection()
            .AddSingleton<ISurfaceContextBroadcaster>(broadcaster)
            .AddSingleton<ISurfaceSessionStore>(new NeverAskedSessionStore())
            .BuildServiceProvider();
        var context = new PluginContext(services, "communication", (_, _, _) => { }, _ => null);

        Assert.NotNull(context.Services.GetService(typeof(ISurfaceContextBroadcaster)));

        // Und die Grenze hält in die andere Richtung: Der Sitzungsspeicher liegt weiter im
        // Host-Namespace und bleibt unerreichbar. Ohne diese Hälfte prüft der Test nur, dass
        // irgendetwas herauskommt — nicht, dass die Regel noch eine Regel ist.
        Assert.Null(context.Services.GetService(typeof(ISurfaceSessionStore)));
    }

    /// <summary>Nur da, damit der Container den Vertrag kennt; aufgerufen wird nichts davon.</summary>
    private sealed class NeverAskedSessionStore : ISurfaceSessionStore
    {
        public Task<SurfaceSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CreateAsync(SurfaceSession session, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task TouchAsync(Guid sessionId, DateTimeOffset seenAtUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> RevokeForSurfaceAsync(
            string workspaceKey, string surfaceKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task SessionResume_IsHandedOutBoundToTheOwningPlugin()
    {
        // Two plugins, one store: the binding is what stops either from redeeming the other's
        // promise, and it has to hold at the point the service is handed out (ADR-018 §2.2).
        var store = new InMemorySessionResumeTicketStore();
        var services = new ServiceCollection()
            .AddSingleton<ISessionResumeTicketStore>(store)
            .AddSingleton<IPluginPayloadProtector>(new DataProtectionPluginPayloadProtector(new EphemeralDataProtectionProvider()))
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
        var services = new ServiceCollection()
            .AddSingleton<IPluginPayloadProtector>(new DataProtectionPluginPayloadProtector(new EphemeralDataProtectionProvider()))
            .BuildServiceProvider();

        Assert.Null(Context(services, "videoconference").Services.GetService(typeof(IHostSessionResumeService)));
    }

    [Fact]
    public void SessionResume_IsAbsentWithoutAPayloadProtector()
    {
        // Rather than storing the payload in the clear. The host never reads it, so it cannot judge
        // how sensitive it is, and a host that cannot protect it should not offer resume at all.
        var services = new ServiceCollection()
            .AddSingleton<ISessionResumeTicketStore>(new InMemorySessionResumeTicketStore())
            .BuildServiceProvider();

        Assert.Null(Context(services, "videoconference").Services.GetService(typeof(IHostSessionResumeService)));
    }

    private static PluginContext Context(IServiceProvider services, string pluginId) =>
        new(services, pluginId, (_, _, _) => { }, _ => null);

    private static IHostSessionResumeService Resume(IServiceProvider services, string pluginId) =>
        (IHostSessionResumeService)Context(services, pluginId).Services.GetService(typeof(IHostSessionResumeService))!;
}
