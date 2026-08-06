using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Application.Calls;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// Shares one trunk's lines between the things that use it. The account limit already stops everyone
/// at the ceiling; this decides who gets there first — so that a dialer working through a campaign
/// cannot take the lines an agent needs to answer with.
/// </summary>
public sealed class CallQuotaLedgerTests
{
    private const string Workspace = "ws-a";
    private const string Account = "acc-1";

    [Fact]
    public void WithoutAQuota_AnOriginIsNotLimited()
    {
        var ledger = new CallQuotaLedger();

        // Quotas divide a trunk on purpose; an operator who configured none wanted no division, not a
        // silent limit of zero.
        var reservations = Enumerable.Range(0, 50)
            .Select(_ => ledger.TryReserve(Workspace, Account, "crm"))
            .ToList();

        Assert.All(reservations, Assert.NotNull);
    }

    [Fact]
    public void AQuotaStopsItsOwnOrigin()
    {
        var ledger = NewLedger(("dialer:campaign-x", 2));

        var first = ledger.TryReserve(Workspace, Account, "dialer:campaign-x");
        var second = ledger.TryReserve(Workspace, Account, "dialer:campaign-x");
        var third = ledger.TryReserve(Workspace, Account, "dialer:campaign-x");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(third);
    }

    [Fact]
    public void AnExhaustedQuota_DoesNotStopAnother()
    {
        var ledger = NewLedger(("dialer:campaign-x", 1), ("crm", 2));
        ledger.TryReserve(Workspace, Account, "dialer:campaign-x");

        // The point of the whole thing: the campaign running dry must not cost the agent their line.
        Assert.Null(ledger.TryReserve(Workspace, Account, "dialer:campaign-x"));
        Assert.NotNull(ledger.TryReserve(Workspace, Account, "crm"));
    }

    [Fact]
    public void ReleasingAReservation_FreesTheLine()
    {
        var ledger = NewLedger(("crm", 1));
        var reservation = ledger.TryReserve(Workspace, Account, "crm");

        reservation!.Dispose();

        Assert.NotNull(ledger.TryReserve(Workspace, Account, "crm"));
    }

    [Fact]
    public void ReleasingTwice_DoesNotHandOutALineTwice()
    {
        var ledger = NewLedger(("crm", 1));
        var reservation = ledger.TryReserve(Workspace, Account, "crm");

        reservation!.Dispose();
        reservation.Dispose();

        Assert.NotNull(ledger.TryReserve(Workspace, Account, "crm"));
        Assert.Null(ledger.TryReserve(Workspace, Account, "crm"));
    }

    [Fact]
    public void QuotasAreScopedToTheirAccount()
    {
        var ledger = NewLedger(("crm", 1));
        ledger.TryReserve(Workspace, Account, "crm");

        // A second trunk has its own lines; exhausting one must not close the other.
        Assert.NotNull(ledger.TryReserve(Workspace, "acc-2", "crm"));
    }

    [Fact]
    public void QuotasAreScopedToTheirWorkspace()
    {
        var ledger = NewLedger(("crm", 1));
        ledger.TryReserve(Workspace, Account, "crm");

        Assert.NotNull(ledger.TryReserve("ws-other", Account, "crm"));
    }

    [Fact]
    public async Task ConcurrentReservations_CannotOvershootTheQuota()
    {
        var ledger = NewLedger(("crm", 5));

        // Checking then incrementing lets N callers all pass the check; the reservation has to be one
        // step. Twenty dials at once is not a stress test, it is a Monday morning.
        var granted = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            Task.Run(() => ledger.TryReserve(Workspace, Account, "crm") is not null)));

        Assert.Equal(5, granted.Count(x => x));
    }

    [Fact]
    public void ReconfiguringQuotas_TakesEffectForNewReservations()
    {
        var ledger = NewLedger(("crm", 1));
        ledger.TryReserve(Workspace, Account, "crm");

        ledger.Configure(Workspace, Account, new Dictionary<string, int> { ["crm"] = 3 });

        // An operator raising a quota expects it to apply now, not after the last call ends.
        Assert.NotNull(ledger.TryReserve(Workspace, Account, "crm"));
    }

    private static CallQuotaLedger NewLedger(params (string Origin, int Limit)[] quotas)
    {
        var ledger = new CallQuotaLedger();
        ledger.Configure(Workspace, Account, quotas.ToDictionary(q => q.Origin, q => q.Limit));
        return ledger;
    }
}
