using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Core.Tests.Communication.Persistence;
using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Infrastructure.Persistence;
using Callora.Plugin.Communication.Infrastructure.Persistence.Stores;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Testcontainers.PostgreSql;
using Xunit;
using LineState = CalloraVoipSdk.Core.Domain.Lines.LineState;

namespace Callora.Core.Tests.Communication;

/// <summary>
/// Composition of the voice surface (B4-deep-2d-3c) against a real Postgres: with an SDK voice runtime
/// supplied, StartAsync provisions a live channel per enabled account into the exported registry; with
/// no runtime it degrades to foundation-only; StopAsync tears the provisioned channels down.
/// </summary>
public sealed class CommunicationVoiceProvisioningWiringTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private PostgresCommunicationDbContextFactory _factory = null!;
    private bool _started;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
        }
        catch (Exception)
        {
            return; // Docker unavailable → tests skip.
        }

        _factory = new PostgresCommunicationDbContextFactory(_postgres.GetConnectionString());
        await _factory.MigrateAsync();
        _started = true;
    }

    public async Task DisposeAsync()
    {
        if (_started)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task StartAsync_WithVoiceRuntime_ProvisionsOnlyEnabledAccountsIntoRegistry()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");
        await SeedAccountAsync("acc-enabled", "ws-voice", enabled: true);
        await SeedAccountAsync("acc-disabled", "ws-voice", enabled: false);
        var context = NewContext(withVoiceRuntime: true);

        await new CommunicationPlugin().StartAsync(context);

        var registry = Registry(context);
        var channel = Assert.Single(registry.GetChannels("ws-voice"));
        Assert.Equal("acc-enabled", channel.ChannelId); // the disabled account is not provisioned
    }

    [SkippableFact]
    public async Task StartAsync_WithoutVoiceRuntime_DegradesToFoundationOnly()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");
        await SeedAccountAsync("acc-1", "ws-none", enabled: true);
        var context = NewContext(withVoiceRuntime: false);

        await new CommunicationPlugin().StartAsync(context);

        // Foundation surface still wired, but no voice channel without a runtime.
        Assert.Contains(typeof(IHostWebSocketEndpointContributor), context.Exports.Keys);
        Assert.Empty(Registry(context).GetChannels("ws-none"));
    }

    [SkippableFact]
    public async Task StopAsync_AfterProvisioning_DeregistersChannels()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");
        await SeedAccountAsync("acc-stop", "ws-stop", enabled: true);
        var context = NewContext(withVoiceRuntime: true);
        var plugin = new CommunicationPlugin();
        await plugin.StartAsync(context);
        Assert.Single(Registry(context).GetChannels("ws-stop"));

        await plugin.StopAsync();

        Assert.Empty(Registry(context).GetChannels("ws-stop"));
    }

    private static ICommunicationChannelRegistry Registry(ServiceCapturingHostPluginContext context) =>
        (ICommunicationChannelRegistry)context.Exports[typeof(ICommunicationChannelRegistry)];

    private ServiceCapturingHostPluginContext NewContext(bool withVoiceRuntime)
    {
        var services = new List<(Type, object)>
        {
            (typeof(IPluginDbContextFactory<CommunicationDbContext>), _factory),
            (typeof(IPluginDataProtector), new FakePluginDataProtector(("pw-ref", "s3cret"))),
        };
        if (withVoiceRuntime)
        {
            services.Add((typeof(ISdkVoiceRuntime),
                new FakeSdkVoiceRuntime { NextLine = new FakePhoneLine { State = LineState.Registered } }));
        }

        return new ServiceCapturingHostPluginContext(services);
    }

    private async Task SeedAccountAsync(string id, string workspaceKey, bool enabled)
    {
        var connection = new SipConnection(
            "sip.example.com", 5060, SipTransport.Udp, SipAccountMode.Register,
            new DigestAuthentication("user", authId: null, passwordSecretRef: "pw-ref"), 600);
        await new EfSipAccountStore(_factory)
            .AddAsync(new SipAccount(id, workspaceKey, "Trunk", connection, maxConcurrentCalls: 1, enabled: enabled));
    }
}

/// <summary>A host plugin context that serves a fixed service set and captures exports.</summary>
internal sealed class ServiceCapturingHostPluginContext : IHostPluginContext, IServiceProvider
{
    private readonly Dictionary<Type, object> _services;

    public ServiceCapturingHostPluginContext(IEnumerable<(Type Type, object Instance)> services) =>
        _services = services.ToDictionary(s => s.Type, s => s.Instance);

    public Dictionary<Type, object> Exports { get; } = [];

    public IServiceProvider Services => this;

    public void Export(Type contractType, object service) => Exports[contractType] = service;

    public object? GetService(Type serviceType) => _services.GetValueOrDefault(serviceType);
}
