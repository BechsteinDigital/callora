using System;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Domain.Calls;
using Callora.Plugin.Communication.Domain.Lines;
using Callora.Plugin.Communication.Infrastructure.Persistence;
using Callora.Plugin.Communication.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Core.Tests.Communication.Persistence;

/// <summary>
/// Persistence-Integrationstest der EF-Stores gegen ein echtes Postgres (Testcontainers): die
/// tatsächliche Migration <c>InitialCommunicationSchema</c> wird angewandt und das
/// Rich-Domain-Mapping (OwnsOne-Connection-VO, String-Enums, private-Ctor-Materialisierung) +
/// die Store-CRUD-/Seam-Logik geprüft. Ohne Docker überspringen die Tests sich selbst. Holt die
/// in B0 verschobene Host-Infra-Coverage (Plugin-DB-Factory/-Migration) zurück.
/// </summary>
[Trait("Category", "Slow")]
public sealed class CommunicationStorePersistenceTests : IAsyncLifetime
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
            // Kein Docker verfügbar — die Tests skippen sich unten.
            return;
        }

        _started = true;
        _factory = new PostgresCommunicationDbContextFactory(_postgres.GetConnectionString());

        // Außerhalb des Docker-catch: ein Migrationsfehler propagiert (Test-Fehler, kein Skip).
        await _factory.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_started)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Migration_CreatesSchema_AndSipAccountRoundTripsWithOwnedConnection()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");

        await using (var db = _factory.CreateDbContext())
        {
            Assert.Equal(CommunicationDbContext.SchemaName, db.Model.FindEntityType(typeof(SipAccount))!.GetSchema());
        }

        var store = new EfSipAccountStore(_factory);
        var account = new SipAccount(
            "acc-1", "ws-a", "Acme Trunk",
            new SipConnection("sip.example.org", 5060, SipTransport.Tls, SipAccountMode.Register, "alice", null, "secret://acc/pw", 3600),
            maxConcurrentCalls: 4, enabled: true);

        await store.AddAsync(account);
        var loaded = await store.GetAsync("ws-a", "acc-1");

        Assert.NotNull(loaded);
        Assert.Equal("Acme Trunk", loaded!.DisplayName);
        Assert.Equal(SipAccountStatus.Connecting, loaded.Status);
        Assert.Equal(4, loaded.MaxConcurrentCalls);
        Assert.Equal("sip.example.org", loaded.Connection.Host);
        Assert.Equal(SipTransport.Tls, loaded.Connection.Transport);
        Assert.Equal(SipAccountMode.Register, loaded.Connection.Mode);
        Assert.Equal("secret://acc/pw", loaded.Connection.PasswordSecretRef);
    }

    [SkippableFact]
    public async Task SipLine_Add_Count_Delete()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");

        var store = new EfSipLineStore(_factory);
        await store.AddAsync(new SipLine("l1", "acc-1", "ws-b", "Main", "sip:a@x", null, true, null));
        await store.AddAsync(new SipLine("l2", "acc-1", "ws-b", "Alt", "sip:b@x", null, true, null));

        Assert.Equal(2, await store.CountByWorkspaceAsync("ws-b"));
        Assert.True(await store.DeleteAsync("ws-b", "l1"));
        Assert.Equal(1, await store.CountByWorkspaceAsync("ws-b"));
        Assert.False(await store.DeleteAsync("ws-b", "missing"));
    }

    [SkippableFact]
    public async Task CallLog_RoundTrips_ThenDeleteByWorkspacePurges()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");

        var store = new EfCallLogStore(_factory);
        var log = CallLog.Start("c1", "ws-c", "acc-1", "l1", CallDirection.Inbound,
            "+49309999999", "sip:alice@x", "ai-agent", null, DateTimeOffset.UnixEpoch);
        log.MarkAnswered(DateTimeOffset.UnixEpoch.AddSeconds(2));
        log.End(DateTimeOffset.UnixEpoch.AddSeconds(42), CallOutcome.Completed, "BYE");
        await store.AddAsync(log);

        var recent = await store.ListRecentAsync("ws-c", 10);
        Assert.Single(recent);
        Assert.Equal(40, recent[0].DurationSeconds);
        Assert.Equal(CallOutcome.Completed, recent[0].Outcome);

        Assert.Equal(1, await store.DeleteByWorkspaceAsync("ws-c"));
        Assert.Empty(await store.ListRecentAsync("ws-c", 10));
    }
}

/// <summary>
/// Test double for <see cref="IPluginDbContextFactory{TContext}"/> pointing at a container
/// Postgres, applying the plugin's real migrations.
/// </summary>
internal sealed class PostgresCommunicationDbContextFactory(string connectionString)
    : IPluginDbContextFactory<CommunicationDbContext>
{
    public CommunicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CommunicationDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(CommunicationDbContext).Assembly.GetName().Name))
            .Options;

        return new CommunicationDbContext(options);
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
