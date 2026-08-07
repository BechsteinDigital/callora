using System;
using System.Linq;
using Callora.Plugin.Communication.Domain.Accounts;
using Xunit;

namespace Callora.Core.Tests.Communication.Domain;

/// <summary>
/// The account's lines, divided between the things that use it. The account limit stays the ceiling;
/// a quota only says how much of it one origin may claim, so a dialer working a queue cannot take the
/// lines an agent needs to answer.
/// </summary>
public sealed class SipAccountCallQuotaTests
{
    [Fact]
    public void AnAccountWithoutQuotas_DividesNothing()
    {
        // Splitting a trunk is deliberate. An operator who configured nothing wanted no split.
        Assert.Empty(Account().CallQuotas);
    }

    [Fact]
    public void ConfiguredQuotas_AreKept()
    {
        var account = Account([new CallQuota("crm", 10), new CallQuota("dialer:campaign-x", 2)]);

        Assert.Equal(
            [("crm", 10), ("dialer:campaign-x", 2)],
            account.CallQuotas.Select(q => (q.Origin, q.MaxConcurrentCalls)));
    }

    [Fact]
    public void Reconfiguring_ReplacesThem()
    {
        var account = Account([new CallQuota("crm", 10)]);

        account.Reconfigure(account.DisplayName, account.Connection, 20, [new CallQuota("crm", 4)]);

        Assert.Equal(4, Assert.Single(account.CallQuotas).MaxConcurrentCalls);
    }

    [Fact]
    public void ReconfiguringWithNone_ClearsThem()
    {
        // An empty list is an operator saying "no split any more". Keeping the old one would leave a
        // limit nobody can see in the configuration they just wrote.
        var account = Account([new CallQuota("crm", 10)]);

        account.Reconfigure(account.DisplayName, account.Connection, 20, []);

        Assert.Empty(account.CallQuotas);
    }

    [Fact]
    public void QuotasMayAddUpToMoreThanTheAccountHas()
    {
        // Dividing exactly would leave lines idle whenever one origin is quiet, which is the opposite
        // of what an operator splitting a trunk wants.
        var account = Account([new CallQuota("crm", 10), new CallQuota("dialer", 10)], maxConcurrentCalls: 12);

        Assert.Equal(20, account.CallQuotas.Sum(q => q.MaxConcurrentCalls));
    }

    [Fact]
    public void TheSameOriginTwice_IsRejected()
    {
        // One of the two would silently win, and which one would depend on ordering.
        Assert.Throws<ArgumentException>(() =>
            Account([new CallQuota("crm", 10), new CallQuota("crm", 2)]));
    }

    [Fact]
    public void AnOriginWithoutAName_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new CallQuota("   ", 5));
    }

    [Fact]
    public void AQuotaOfZero_IsRejected()
    {
        // Zero lines is not a quota, it is a ban — and an origin that should not call does not get a
        // quota, it gets no code path.
        Assert.Throws<ArgumentOutOfRangeException>(() => new CallQuota("crm", 0));
    }

    [Fact]
    public void TheOriginIsTrimmed()
    {
        // Matched ordinally against what a plugin passes, so a stray space would quietly never match.
        Assert.Equal("crm", new CallQuota(" crm ", 5).Origin);
    }

    [Fact]
    public void OriginsDifferingOnlyInCase_AreDifferentOrigins()
    {
        // Ordinal, like the ledger's own key: a case-folding surprise there would be hard to see.
        var account = Account([new CallQuota("crm", 10), new CallQuota("CRM", 2)]);

        Assert.Equal(2, account.CallQuotas.Count);
    }

    private static SipAccount Account(CallQuota[]? quotas = null, int maxConcurrentCalls = 10) =>
        new(
            "a1",
            "ws-a",
            "Berlin Trunk",
            new SipConnection(
                "sip.example.com",
                5060,
                SipTransport.Udp,
                SipAccountMode.Register,
                new DigestAuthentication("user", null, "secret-ref"),
                registrationExpirySeconds: 300),
            maxConcurrentCalls,
            enabled: true,
            quotas);
}
