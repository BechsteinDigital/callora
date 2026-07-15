using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// End-to-end proof of the plugin-owned EF database against a real Postgres
/// (PLAT-260): applies the plugin migration into its schema, exercises the
/// EF-backed SIP account store, and drops the schema on uninstall. Requires
/// Docker; skipped automatically when unavailable so the normal suite stays
/// green.
/// </summary>
[Trait("Category", "Slow")]
public sealed class PluginDatabaseIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private bool _started;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
            _started = true;
        }
        catch (Exception)
        {
            // No Docker available — tests below skip themselves.
            _started = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_started)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Migrate_CreatesPluginSchema_AndSipAccountStoreRoundTrips()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        var factory = BuildFactory();
        await factory.MigrateAsync();

        // Schema and table exist.
        await using (var db = factory.CreateDbContext())
        {
            var schema = db.Model.FindEntityType(typeof(SipAccount))!.GetSchema();
            Assert.Equal(VoipDbContext.SchemaName, schema);
            Assert.Empty(await db.SipAccounts.ToListAsync());
        }

        var store = new EfSipAccountStore(factory, new PassthroughDataProtector());

        var created = await store.CreateAsync(
            "workspace-a",
            new UpsertSipAccountRequest("alice", "example.org", "Alice", "s3cret", true));
        Assert.Equal("s3cret", created.Secret);

        var fetched = await store.GetAsync("workspace-a", created.SipAccountId);
        Assert.NotNull(fetched);
        Assert.Equal("Alice", fetched!.DisplayName);

        var updated = await store.UpdateAsync(
            "workspace-a",
            created.SipAccountId,
            new UpsertSipAccountRequest("alice", "example.org", "Alice Renamed", "s3cret", false));
        Assert.Equal("Alice Renamed", updated!.DisplayName);
        Assert.False(updated.IsActive);

        Assert.Single(await store.ListAsync("workspace-a"));
        Assert.Contains("workspace-a", await store.ListWorkspaceKeysAsync());

        Assert.True(await store.DeleteAsync("workspace-a", created.SipAccountId));
        Assert.Empty(await store.ListAsync("workspace-a"));
    }

    [SkippableFact]
    public async Task DropSchema_RemovesAllPluginTables()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        var factory = BuildFactory();
        await factory.MigrateAsync();

        await using var db = factory.CreateDbContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        var schema = PluginSchemaName.TryResolve("communication")!;
        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = "DROP SCHEMA IF EXISTS \"" + schema + "\" CASCADE;";
            await drop.ExecuteNonQueryAsync();
        }

        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @s;";
        var param = check.CreateParameter();
        param.ParameterName = "s";
        param.Value = schema;
        check.Parameters.Add(param);
        var remaining = (long)(await check.ExecuteScalarAsync())!;
        Assert.Equal(0, remaining);
    }

    [SkippableFact]
    public async Task PurgeContributor_ErasesWorkspaceData_LeavingOtherWorkspaces()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        var factory = BuildFactory();
        await factory.MigrateAsync();

        var store = new EfSipAccountStore(factory, new PassthroughDataProtector());
        await store.CreateAsync("workspace-a",
            new UpsertSipAccountRequest("a", "example.org", "A", "s", true));
        await store.CreateAsync("workspace-b",
            new UpsertSipAccountRequest("b", "example.org", "B", "s", true));

        await using (var seed = factory.CreateDbContext())
        {
            seed.CallLogs.Add(NewCallLog("workspace-a"));
            seed.CallLogs.Add(NewCallLog("workspace-b"));
            await seed.SaveChangesAsync();
        }

        await new CommunicationWorkspaceDataPurgeContributor(factory).PurgeWorkspaceAsync("workspace-a");

        await using var db = factory.CreateDbContext();
        Assert.Empty(await db.CallLogs.Where(x => x.WorkspaceKey == "workspace-a").ToListAsync());
        Assert.Empty(await db.SipAccounts.Where(x => x.WorkspaceKey == "workspace-a").ToListAsync());
        Assert.Single(await db.CallLogs.Where(x => x.WorkspaceKey == "workspace-b").ToListAsync());
        Assert.Single(await db.SipAccounts.Where(x => x.WorkspaceKey == "workspace-b").ToListAsync());
    }

    private static CallLog NewCallLog(string workspaceKey) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceKey = workspaceKey,
        CallId = "call-1",
        ChannelId = "chan-1",
        Direction = "inbound",
        TargetValue = "sip:x@example.org",
        StartedAtUtc = DateTimeOffset.UtcNow,
        EndedAtUtc = DateTimeOffset.UtcNow
    };

    private PluginDbContextFactory<VoipDbContext> BuildFactory()
    {
        var provider = new NpgsqlPluginDbContextProvider(
            new BackendHostOptions { DatabaseConnectionString = _postgres.GetConnectionString() });
        return new PluginDbContextFactory<VoipDbContext>(provider, "communication");
    }

    private sealed class PassthroughDataProtector : Callora.Host.PluginContracts.Application.Secrets.IPluginDataProtector
    {
        public string Protect(string scope, string plaintext) => plaintext;

        public bool TryUnprotect(string scope, string protectedValue, out string plaintext)
        {
            plaintext = protectedValue;
            return true;
        }
    }
}
