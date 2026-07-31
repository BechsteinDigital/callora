using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Core.Tests.Communication.Persistence;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Persistence;
using Callora.Plugin.Communication.Infrastructure.Persistence.Stores;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;
using DigestAuthentication = Callora.Plugin.Communication.Domain.Accounts.DigestAuthentication;
using DomainSipAccount = Callora.Plugin.Communication.Domain.Accounts.SipAccount;
using SipAccountMode = Callora.Plugin.Communication.Domain.Accounts.SipAccountMode;
using SipConnection = Callora.Plugin.Communication.Domain.Accounts.SipConnection;
using SipTransport = Callora.Plugin.Communication.Domain.Accounts.SipTransport;

namespace Callora.Core.Tests.Communication.Integration;

/// <summary>
/// H2 — the monolithic full-plugin voice E2E, opt-in via CALLORA_ASTERISK_TESTS=1 (needs a running
/// Asterisk) and Docker (Testcontainers Postgres). It boots the REAL <see cref="CommunicationPlugin.StartAsync"/>
/// path — not the connector directly: a persisted, encrypted SIP account in a real plugin database, the
/// config switch <c>Voice:Enabled=true</c>, no injected runtime (the plugin self-builds the
/// SDK client) — and asserts the whole chain lands: real digest registration → a healthy voice channel
/// in the registry → <c>communication.voice</c> granted for the workspace. Closes the "no single system
/// test" gap. Start Asterisk first (see ops/spikes/asterisk-b4deep3).
/// </summary>
[Trait("Category", "Asterisk")]
public sealed class AsteriskFullPluginE2ETests : IAsyncLifetime
{
    private const string Workspace = "ws-e2e";
    private const string SipUser = "callora";
    private const string SipPassword = "callora";

    private static bool Enabled => Environment.GetEnvironmentVariable("CALLORA_ASTERISK_TESTS") == "1";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private PostgresCommunicationDbContextFactory _factory = null!;
    private bool _postgresStarted;

    public async Task InitializeAsync()
    {
        if (!Enabled)
        {
            return; // opt-in; the test skips itself without a running Asterisk
        }

        try
        {
            await _postgres.StartAsync();
        }
        catch (Exception)
        {
            return; // no Docker → the test skips itself
        }

        _postgresStarted = true;
        _factory = new PostgresCommunicationDbContextFactory(_postgres.GetConnectionString());
        await _factory.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgresStarted)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task FullPluginStartup_RegistersPersistedAccount_GrantsCommunicationVoice()
    {
        Skip.IfNot(Enabled, "Set CALLORA_ASTERISK_TESTS=1 with a running Asterisk to run this.");
        Skip.IfNot(_postgresStarted, "Docker/Postgres not available.");

        // Persist an enabled digest account (password protected, only the reference stored) pointing at
        // the local Asterisk — exactly what an operator would create through the admin API.
        var dataProtector = new InMemoryDataProtector();
        var passwordRef = dataProtector.Protect(CommunicationPlugin.Id, SipPassword);
        var connection = new SipConnection(
            "127.0.0.1", 5060, SipTransport.Udp, SipAccountMode.Register,
            new DigestAuthentication(SipUser, authId: null, passwordSecretRef: passwordRef), registrationExpirySeconds: 600);
        await new EfSipAccountStore(_factory).AddAsync(
            new DomainSipAccount("acc-e2e", Workspace, "E2E Line", connection, maxConcurrentCalls: 1, enabled: true));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Voice:Enabled"] = "true" })
            .Build();
        var context = new E2EHostPluginContext(_factory, dataProtector, configuration);

        // Bound the real registration so a hung Asterisk fails the test instead of blocking forever
        // (parity with the sibling Asterisk tests).
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var plugin = new CommunicationPlugin();
        await plugin.StartAsync(context, cts.Token);
        try
        {
            var registry = (ICommunicationChannelRegistry)context.Exports[typeof(ICommunicationChannelRegistry)];
            var source = (IRuntimeCapabilitySource)context.Exports[typeof(IRuntimeCapabilitySource)];

            // The self-built runtime registered the persisted account → a healthy voice channel exists...
            var channels = registry.GetChannelsByCapability(Workspace, CommunicationCapabilities.Voice);
            var channel = Assert.Single(channels);
            Assert.Equal(ChannelHealth.Up, channel.Health);

            // ...and the capability source grants communication.voice for that workspace.
            Assert.Contains(
                new RuntimeCapabilityGrant(CommunicationCapabilities.Voice, Workspace),
                source.CurrentGrants);
        }
        finally
        {
            await plugin.StopAsync();
        }
    }

    /// <summary>A data protector that round-trips references in-memory (stands in for the host's).</summary>
    private sealed class InMemoryDataProtector : IPluginDataProtector
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public string Protect(string pluginId, string plaintext)
        {
            var reference = $"protected:{_secrets.Count}";
            _secrets[reference] = plaintext;
            return reference;
        }

        public bool TryUnprotect(string pluginId, string protectedValue, out string plaintext) =>
            _secrets.TryGetValue(protectedValue, out plaintext!);
    }

    /// <summary>Host plugin context wiring the real DB factory, data protector and configuration in.</summary>
    private sealed class E2EHostPluginContext(
        IPluginDbContextFactory<CommunicationDbContext> dbContextFactory,
        IPluginDataProtector dataProtector,
        IConfiguration configuration) : IHostPluginContext, IServiceProvider
    {
        public Dictionary<Type, object> Exports { get; } = [];

        public IServiceProvider Services => this;

        public IConfiguration PluginConfiguration => configuration;

        public void Export(Type contractType, object service) => Exports[contractType] = service;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IPluginDbContextFactory<CommunicationDbContext>))
            {
                return dbContextFactory;
            }

            if (serviceType == typeof(IPluginDataProtector))
            {
                return dataProtector;
            }

            if (serviceType == typeof(IConfiguration))
            {
                return configuration;
            }

            return null;
        }
    }
}
