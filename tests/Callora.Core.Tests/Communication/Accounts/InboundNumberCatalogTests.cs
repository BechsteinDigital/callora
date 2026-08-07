using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Domain.Accounts;
using Xunit;

namespace Callora.Core.Tests.Communication.Accounts;

/// <summary>
/// Which numbers can reach a workspace at all. A consumer that owns particular numbers has to name
/// them, and letting an operator type them from memory is how a conference line ends up listening for
/// a number the trunk never delivers.
/// </summary>
public sealed class InboundNumberCatalogTests
{
    [Fact]
    public async Task TheNumbersOfEveryAccount_AreListed()
    {
        var catalog = Catalog(
            Account("a1", "ws-a", "Berlin Trunk", ["+493012345678", "+493012345679"]),
            Account("a2", "ws-a", "München Trunk", ["+498912345678"]));

        var numbers = await catalog.ListAsync("ws-a");

        Assert.Equal(
            ["+493012345678", "+493012345679", "+498912345678"],
            numbers.Select(n => n.Number));
    }

    [Fact]
    public async Task EachNumber_SaysWhichTrunkItArrivesOn()
    {
        // An operator picking a number needs to see whose line it is; two trunks with consecutive
        // blocks are otherwise indistinguishable in a dropdown.
        var catalog = Catalog(Account("a1", "ws-a", "Berlin Trunk", ["+493012345678"]));

        var number = Assert.Single(await catalog.ListAsync("ws-a"));

        Assert.Equal(("a1", "Berlin Trunk"), (number.ChannelId, number.ChannelDisplayName));
    }

    [Fact]
    public async Task AnotherWorkspacesNumbers_AreNotListed()
    {
        var catalog = Catalog(
            Account("a1", "ws-a", "Berlin Trunk", ["+493012345678"]),
            Account("b1", "ws-b", "Fremder Trunk", ["+493087654321"]));

        Assert.Equal(["+493012345678"], (await catalog.ListAsync("ws-a")).Select(n => n.Number));
    }

    [Fact]
    public async Task AnAccountWithoutAWhitelist_ContributesNothing()
    {
        // A trunk that accepts every number cannot say which numbers those are, so there is nothing to
        // offer — an operator on such a trunk types the number and takes responsibility for it.
        var catalog = Catalog(Account("a1", "ws-a", "Offener Trunk", []));

        Assert.Empty(await catalog.ListAsync("ws-a"));
    }

    [Fact]
    public async Task ADisabledAccount_StillOffersItsNumbers()
    {
        // The number exists on the contract with the carrier whether or not the trunk is switched on
        // right now, and assigning it before enabling the line is the normal order of work.
        var catalog = Catalog(Account("a1", "ws-a", "Noch aus", ["+493012345678"], enabled: false));

        Assert.Single(await catalog.ListAsync("ws-a"));
    }

    [Fact]
    public async Task TheSameNumberOnTwoTrunks_IsShownTwice()
    {
        // Broken configuration, and hiding one of the two would leave an operator wondering why calls
        // land on the wrong line.
        var catalog = Catalog(
            Account("a1", "ws-a", "Trunk A", ["+493012345678"]),
            Account("a2", "ws-a", "Trunk B", ["+493012345678"]));

        Assert.Equal(2, (await catalog.ListAsync("ws-a")).Count);
    }

    private static InboundNumberCatalog Catalog(params SipAccount[] accounts) =>
        new(new FakeAccountStore(accounts));

    private static SipAccount Account(
        string id,
        string workspaceKey,
        string displayName,
        string[] inboundNumbers,
        bool enabled = true) =>
        new(
            id,
            workspaceKey,
            displayName,
            new SipConnection(
                "sip.example.com",
                5060,
                SipTransport.Udp,
                SipAccountMode.Trunk,
                IpAuthentication.Instance,
                registrationExpirySeconds: null,
                outboundProxy: null,
                inboundNumbers: inboundNumbers),
            maxConcurrentCalls: 4,
            enabled);

    /// <summary>Hands back the seeded accounts of one workspace; nothing else is exercised here.</summary>
    private sealed class FakeAccountStore(SipAccount[] accounts) : ISipAccountStore
    {
        public Task<IReadOnlyList<SipAccount>> ListAsync(string workspaceKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SipAccount>>(
                [.. accounts.Where(a => a.WorkspaceKey == workspaceKey)]);

        public Task<IReadOnlyList<SipAccount>> ListEnabledAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SipAccount>>([.. accounts.Where(a => a.Enabled)]);

        public Task<SipAccount?> GetAsync(string workspaceKey, string accountId, CancellationToken ct = default) =>
            Task.FromResult(accounts.FirstOrDefault(a => a.WorkspaceKey == workspaceKey && a.Id == accountId));

        public Task AddAsync(SipAccount account, CancellationToken ct = default) => throw new NotSupportedException();

        public Task UpdateAsync(SipAccount account, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(string workspaceKey, string accountId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
