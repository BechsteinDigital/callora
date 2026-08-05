using Callora.Core.Application.Security;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Single use is the whole point of a resume promise (ADR-018 §2.2), and single use is a database
/// property here. These run against a real Postgres because an in-memory double cannot prove the
/// delete-and-return actually races correctly.
/// </summary>
[Trait("Category", "Slow")]
public sealed class SessionResumeTicketStoreIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
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
    public async Task ATicketRoundTripsWithItsPayload()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSessionResumeTicketStore(context);
        var secret = SingleUseSecret.Create();

        await store.CreateAsync(Ticket(secret));
        var consumed = await store.ConsumeAsync(SingleUseSecret.Hash(secret), "videoconference");

        Assert.Equal("conference", consumed!.SessionKind);
        Assert.Equal("""{"room":"r-1","participant":"p-9"}""", consumed.Payload);
        Assert.Equal("ws-a", consumed.WorkspaceKey);
    }

    [SkippableFact]
    public async Task ConsumingTwiceYieldsTheTicketOnlyOnce()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSessionResumeTicketStore(context);
        var secret = SingleUseSecret.Create();
        await store.CreateAsync(Ticket(secret));

        Assert.NotNull(await store.ConsumeAsync(SingleUseSecret.Hash(secret), "videoconference"));
        Assert.Null(await store.ConsumeAsync(SingleUseSecret.Hash(secret), "videoconference"));
    }

    [SkippableFact]
    public async Task AForeignPluginNeitherReadsNorConsumesIt()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSessionResumeTicketStore(context);
        var secret = SingleUseSecret.Create();
        await store.CreateAsync(Ticket(secret));

        Assert.Null(await store.ConsumeAsync(SingleUseSecret.Hash(secret), "communication"));

        // Ownership is part of the lookup rather than a check afterwards, so the failed attempt must
        // leave the ticket intact for whoever actually issued it.
        Assert.NotNull(await store.ConsumeAsync(SingleUseSecret.Hash(secret), "videoconference"));
    }

    [SkippableFact]
    public async Task TheSecretItselfIsNeverStored()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSessionResumeTicketStore(context);
        var secret = SingleUseSecret.Create();

        await store.CreateAsync(Ticket(secret));

        var stored = await context.SessionResumeTickets.AsNoTracking().SingleAsync();
        Assert.NotEqual(secret, stored.TokenHash);
        Assert.Equal(SingleUseSecret.Hash(secret), stored.TokenHash);
    }

    [SkippableFact]
    public async Task PurgingDropsExpiredTicketsOnly()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await ContextAsync();
        var store = new EfSessionResumeTicketStore(context);
        var live = SingleUseSecret.Create();
        var stale = SingleUseSecret.Create();
        await store.CreateAsync(Ticket(live, expiresAtUtc: Now.AddMinutes(1)));
        await store.CreateAsync(Ticket(stale, expiresAtUtc: Now.AddSeconds(-1)));

        Assert.Equal(1, await store.PurgeExpiredAsync(Now));
        Assert.NotNull(await store.ConsumeAsync(SingleUseSecret.Hash(live), "videoconference"));
    }

    private async Task<HostPersistenceDbContext> ContextAsync()
    {
        var context = new HostPersistenceDbContext(
            new DbContextOptionsBuilder<HostPersistenceDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static SessionResumeTicketRecord Ticket(string secret, DateTimeOffset? expiresAtUtc = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TokenHash = SingleUseSecret.Hash(secret),
            PluginId = "videoconference",
            SessionKind = "conference",
            WorkspaceKey = "ws-a",
            Payload = """{"room":"r-1","participant":"p-9"}""",
            IssuedAtUtc = Now,
            ExpiresAtUtc = expiresAtUtc ?? Now.AddMinutes(1),
        };
}
