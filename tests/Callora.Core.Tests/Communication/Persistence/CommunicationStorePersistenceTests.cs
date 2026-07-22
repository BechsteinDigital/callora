using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Domain.Calls;
using Callora.Plugin.Communication.Domain.Lines;
using Callora.Plugin.Communication.Domain.Streaming;
using Callora.Plugin.Communication.Infrastructure.Persistence;
using Callora.Plugin.Communication.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Core.Tests.Communication.Persistence;

/// <summary>
/// Persistence-Integrationstest der EF-Stores gegen ein echtes Postgres (Testcontainers): die
/// tatsächlichen Migrationen (<c>InitialCommunicationSchema</c>, <c>AddMediaStreamSessions</c>)
/// werden angewandt und das Rich-Domain-Mapping (OwnsOne-VOs SipConnection/AudioFormat,
/// String-Enums, private-Ctor-Materialisierung) + die Store-CRUD-/Seam-Logik geprüft. Ohne
/// Docker überspringen die Tests sich selbst. Holt die in B0 verschobene Host-Infra-Coverage
/// (Plugin-DB-Factory/-Migration) zurück.
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
            new SipConnection("sip.example.org", 5060, SipTransport.Tls, SipAccountMode.Register,
                new DigestAuthentication("alice", null, "secret://acc/pw"), 3600),
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
        Assert.Equal("secret://acc/pw", ((DigestAuthentication)loaded.Connection.Authentication).PasswordSecretRef);
    }

    [SkippableFact]
    public async Task SipAccount_IpAuthenticatedTrunk_RoundTrips_WithoutCredentials()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");

        var store = new EfSipAccountStore(_factory);
        var account = new SipAccount(
            "acc-trunk", "ws-t", "IP Trunk",
            new SipConnection("trunk.example.org", 5060, SipTransport.Udp, SipAccountMode.Trunk,
                IpAuthentication.Instance, registrationExpirySeconds: null),
            maxConcurrentCalls: 8, enabled: true);
        await store.AddAsync(account);

        var loaded = await store.GetAsync("ws-t", "acc-trunk");

        Assert.NotNull(loaded);
        Assert.Equal(SipAccountMode.Trunk, loaded!.Connection.Mode);
        Assert.IsType<IpAuthentication>(loaded.Connection.Authentication);
        Assert.Null(loaded.Connection.RegistrationExpirySeconds);
    }

    [SkippableFact]
    public async Task SipLine_Add_Count_Delete()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");

        await AddAccountAsync("acc-1", "ws-b");
        var store = new EfSipLineStore(_factory);
        await store.AddAsync(new SipLine("l1", "acc-1", "ws-b", "Main", "sip:a@x", null, true, null));
        await store.AddAsync(new SipLine("l2", "acc-1", "ws-b", "Alt", "sip:b@x", null, true, null));

        Assert.Equal(2, await store.CountByWorkspaceAsync("ws-b"));
        Assert.True(await store.DeleteAsync("ws-b", "l1"));
        Assert.Equal(1, await store.CountByWorkspaceAsync("ws-b"));
        Assert.False(await store.DeleteAsync("ws-b", "missing"));
    }

    [SkippableFact]
    public async Task ListByAccount_IsWorkspaceScoped_NoCrossTenantLeak()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");

        await AddAccountAsync("acc-scoped", "ws-owner");
        var store = new EfSipLineStore(_factory);
        await store.AddAsync(new SipLine("l-own", "acc-scoped", "ws-owner", "Main", "sip:o@x", null, true, null));

        // The owning workspace sees its line; another workspace passing the same account id sees nothing.
        Assert.Single(await store.ListByAccountAsync("ws-owner", "acc-scoped"));
        Assert.Empty(await store.ListByAccountAsync("ws-intruder", "acc-scoped"));
    }

    [SkippableFact]
    public async Task Line_CannotReferenceAccountInAnotherWorkspace()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");

        await AddAccountAsync("acc-a", "ws-a");
        var store = new EfSipLineStore(_factory);

        // A line in ws-b referencing an account that lives in ws-a is rejected by the composite FK.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            store.AddAsync(new SipLine("l-cross", "acc-a", "ws-b", "Cross", "sip:x@x", null, true, null)));
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

    [SkippableFact]
    public async Task MediaStreamSession_RoundTrips_ByToken_ThenActivateCloseArePersisted()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");

        var store = new EfMediaStreamSessionStore(_factory);
        var session = new MediaStreamSession(
            "sess-1", "call-1", "ws-s", "ai-agent", "tok-xyz",
            AudioFormat.G711Ulaw8k20ms, MediaStreamDirection.Bidirectional, DateTimeOffset.UnixEpoch);
        await store.AddAsync(session);

        var byToken = await store.GetByConnectTokenAsync("tok-xyz");
        Assert.NotNull(byToken);
        Assert.Equal("sess-1", byToken!.Id);
        Assert.Equal(MediaStreamSessionStatus.Pending, byToken.Status);
        Assert.Equal(AudioCodec.G711Ulaw, byToken.Format.Codec);
        Assert.Equal(8000, byToken.Format.SampleRateHz);

        byToken.Activate(DateTimeOffset.UnixEpoch.AddSeconds(2));
        byToken.Close(DateTimeOffset.UnixEpoch.AddSeconds(9));
        await store.UpdateAsync(byToken);

        var reloaded = await store.GetAsync("ws-s", "sess-1");
        Assert.NotNull(reloaded);
        Assert.Equal(MediaStreamSessionStatus.Closed, reloaded!.Status);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(2), reloaded.StartedAt);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(9), reloaded.EndedAt);
    }

    [SkippableFact]
    public async Task TryActivateByConnectToken_IsSingleUse_UnderConcurrentDoubleConnect()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");

        var store = new EfMediaStreamSessionStore(_factory);
        await store.AddAsync(new MediaStreamSession(
            "sess-race", "call-r", "ws-race", "ai-agent", "tok-race",
            AudioFormat.G711Ulaw8k20ms, MediaStreamDirection.Bidirectional, DateTimeOffset.UtcNow));

        var now = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromMinutes(2);

        // Eight consumers race to redeem the same token; the atomic CAS must let exactly one win.
        var outcomes = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => store.TryActivateByConnectTokenAsync("tok-race", now, ttl)));

        Assert.Equal(1, outcomes.Count(session => session is not null));

        var reloaded = await store.GetByConnectTokenAsync("tok-race");
        Assert.Equal(MediaStreamSessionStatus.Active, reloaded!.Status);
    }

    [SkippableFact]
    public async Task WorkspaceDataPurger_AtomicallyErasesAllFourTables()
    {
        Skip.IfNot(_started, "Docker/Postgres nicht verfügbar.");

        var accountStore = new EfSipAccountStore(_factory);
        var lineStore = new EfSipLineStore(_factory);
        var callLogStore = new EfCallLogStore(_factory);
        var sessionStore = new EfMediaStreamSessionStore(_factory);

        await accountStore.AddAsync(new SipAccount(
            "acc-p", "ws-purge", "Purge",
            new SipConnection("h", 5060, SipTransport.Udp, SipAccountMode.Register,
                new DigestAuthentication("u", null, "s://p"), 3600),
            maxConcurrentCalls: 2, enabled: true));
        await lineStore.AddAsync(new SipLine("l-p", "acc-p", "ws-purge", "L", "sip:p@x", null, true, null));
        var log = CallLog.Start("c-p", "ws-purge", "acc-p", "l-p", CallDirection.Inbound,
            "+49301111111", "sip:p@x", null, null, DateTimeOffset.UnixEpoch);
        log.End(DateTimeOffset.UnixEpoch.AddSeconds(5), CallOutcome.Missed, null);
        await callLogStore.AddAsync(log);
        await sessionStore.AddAsync(new MediaStreamSession(
            "sess-p", "c-p", "ws-purge", "ai-agent", "tok-purge",
            AudioFormat.G711Ulaw8k20ms, MediaStreamDirection.Bidirectional, DateTimeOffset.UnixEpoch));

        await new CommunicationWorkspaceDataPurger(_factory).PurgeAsync("ws-purge");

        Assert.Empty(await accountStore.ListAsync("ws-purge"));
        Assert.Equal(0, await lineStore.CountByWorkspaceAsync("ws-purge"));
        Assert.Empty(await callLogStore.ListRecentAsync("ws-purge", 10));
        Assert.Null(await sessionStore.GetByConnectTokenAsync("tok-purge"));
    }

    private async Task AddAccountAsync(string accountId, string workspaceKey)
    {
        var accountStore = new EfSipAccountStore(_factory);
        await accountStore.AddAsync(new SipAccount(
            accountId, workspaceKey, "Trunk",
            new SipConnection("h", 5060, SipTransport.Udp, SipAccountMode.Register,
                new DigestAuthentication("u", null, "s://p"), 3600),
            maxConcurrentCalls: 2, enabled: true));
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
