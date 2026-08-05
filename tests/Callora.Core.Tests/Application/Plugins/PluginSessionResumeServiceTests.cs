using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// A resume promise is what turns a dropped connection into a pause instead of an ending
/// (ADR-018 §2.2). These cover what the host guarantees about it: who may redeem, how often, for how
/// long, and how much it may carry.
/// </summary>
public sealed class PluginSessionResumeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task APayloadComesBackVerbatim()
    {
        var (service, _, _) = NewService();

        var ticket = await service.IssueAsync("conference", """{"room":"r-1","participant":"p-9"}""", TimeSpan.FromMinutes(5));
        var resumed = await service.RedeemAsync(ticket.Token);

        // The host stores it, never reads it: the payload's meaning is the plugin's alone.
        Assert.Equal("""{"room":"r-1","participant":"p-9"}""", resumed!.Payload);
        Assert.Equal("conference", resumed.SessionKind);
        Assert.Equal(Now, resumed.IssuedAtUtc);
    }

    [Fact]
    public async Task ATokenIsRedeemableOnce()
    {
        var (service, _, _) = NewService();
        var ticket = await service.IssueAsync("conference", "p", TimeSpan.FromMinutes(5));

        Assert.NotNull(await service.RedeemAsync(ticket.Token));

        // A client that wants to stay resumable asks for a fresh ticket, which is what keeps an
        // intercepted token worthless the moment it has been spent.
        Assert.Null(await service.RedeemAsync(ticket.Token));
    }

    [Fact]
    public async Task AnotherPluginCannotRedeemIt()
    {
        var store = new InMemorySessionResumeTicketStore();
        var clock = new FakeTimeProvider(Now);
        var mine = NewService(store, clock, "videoconference").Service;
        var theirs = NewService(store, clock, "communication").Service;
        var ticket = await mine.IssueAsync("conference", "p", TimeSpan.FromMinutes(5));

        Assert.Null(await theirs.RedeemAsync(ticket.Token));

        // And the rightful owner still has it: a foreign attempt must not consume the ticket either.
        Assert.NotNull(await mine.RedeemAsync(ticket.Token));
    }

    [Fact]
    public async Task AnExpiredTicketIsRefused()
    {
        var (service, _, clock) = NewService();
        var ticket = await service.IssueAsync("conference", "p", TimeSpan.FromMinutes(5));

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        Assert.Null(await service.RedeemAsync(ticket.Token));
    }

    [Fact]
    public async Task ALifetimeBeyondTheHostMaximumIsClamped()
    {
        var (service, _, _) = NewService();

        var ticket = await service.IssueAsync("conference", "p", TimeSpan.FromDays(30));

        // The line between a reconnect window and a standing bearer credential is exactly this.
        Assert.Equal(Now.AddMinutes(15), ticket.ExpiresAtUtc);
    }

    [Fact]
    public async Task AnOversizedPayloadIsRefusedRatherThanTruncated()
    {
        var (service, store, _) = NewService();

        // Truncating would only surface on the reconnect, as a payload the plugin cannot rebuild from.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.IssueAsync("conference", new string('x', 4097), TimeSpan.FromMinutes(5)));
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task TheSecretItselfIsNeverStored()
    {
        var (service, store, _) = NewService();

        var ticket = await service.IssueAsync("conference", "p", TimeSpan.FromMinutes(5));

        var stored = Assert.Single(store.Records);
        Assert.NotEqual(ticket.Token, stored.TokenHash);
        Assert.Equal(SingleUseSecret.Hash(ticket.Token), stored.TokenHash);
    }

    [Fact]
    public async Task RevokingGivesUpTheRightToComeBack()
    {
        var (service, store, _) = NewService();
        var ticket = await service.IssueAsync("conference", "p", TimeSpan.FromMinutes(5));

        // A deliberate hang-up and a dropped connection look identical to the server; only the
        // former should spend the ticket.
        await service.RevokeAsync(ticket.Token);

        Assert.Null(await service.RedeemAsync(ticket.Token));
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task AnUnknownTokenIsJustNull()
    {
        var (service, _, _) = NewService();

        // Unknown, spent, expired and foreign all answer the same on purpose: a probe learns nothing
        // from which one it hit.
        Assert.Null(await service.RedeemAsync("not-a-token"));
        Assert.Null(await service.RedeemAsync(""));
    }

    [Fact]
    public async Task AWorkspaceIsCarriedWhenTheSessionHasOne()
    {
        var (service, _, _) = NewService();

        var withWorkspace = await service.IssueAsync("conference", "p", TimeSpan.FromMinutes(5), "ws-a");
        var without = await service.IssueAsync("conference", "p", TimeSpan.FromMinutes(5));

        Assert.Equal("ws-a", (await service.RedeemAsync(withWorkspace.Token))!.WorkspaceKey);
        Assert.Null((await service.RedeemAsync(without.Token))!.WorkspaceKey);
    }

    private static (IHostSessionResumeService Service, InMemorySessionResumeTicketStore Store, FakeTimeProvider Clock)
        NewService(
            InMemorySessionResumeTicketStore? store = null,
            FakeTimeProvider? clock = null,
            string pluginId = "videoconference")
    {
        store ??= new InMemorySessionResumeTicketStore();
        clock ??= new FakeTimeProvider(Now);
        return (new PluginSessionResumeService(store, clock, new CalloraHostingOptions(), pluginId), store, clock);
    }
}
