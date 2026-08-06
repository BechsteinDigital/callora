using System;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// Collecting a multi-digit entry from single DTMF tones — a conference PIN, an IVR menu, a customer
/// number. Every consumer would otherwise rebuild the same three awkward parts: duplicates, two
/// threads, and an ending that is not obvious.
/// </summary>
public sealed class CallDtmfCollectorTests
{
    private const string Workspace = "ws-a";
    private const string CallId = "call-1";

    private static readonly DtmfCollectOptions FourDigits =
        new(Length: 4, InterDigitTimeout: TimeSpan.FromSeconds(5));

    [Fact]
    public async Task AFullLengthEntry_CompletesWithoutASubmitKey()
    {
        var (collector, call, _) = New();
        var collecting = collector.CollectAsync(Workspace, CallId, FourDigits);

        Press(call, "1234");

        var entry = await collecting;
        // Most callers never press #, so waiting for one only burns the pause timeout.
        Assert.Equal(DtmfEntryOutcome.Completed, entry.Outcome);
        Assert.Equal("1234", entry.Digits);
    }

    [Fact]
    public async Task ARepeatedTone_DoesNotBecomeASecondDigit()
    {
        var (collector, call, time) = New();
        var collecting = collector.CollectAsync(Workspace, CallId, FourDigits);

        // In-band echo and RFC 4733 retransmissions report the same keypress again, milliseconds
        // apart. Two real presses are never that close.
        call.ReceiveDtmf('1');
        call.ReceiveDtmf('1');
        time.Advance(TimeSpan.FromMilliseconds(300));
        Press(call, "234");

        var entry = await collecting;
        Assert.Equal("1234", entry.Digits);
    }

    [Fact]
    public async Task TheSameDigitPressedTwiceDeliberately_CountsTwice()
    {
        var (collector, call, time) = New();
        var collecting = collector.CollectAsync(Workspace, CallId, FourDigits);

        // The other side of de-bouncing: "1122" must stay "1122". A caller pressing the same key
        // again takes far longer than an echo arrives.
        call.ReceiveDtmf('1');
        time.Advance(TimeSpan.FromMilliseconds(300));
        call.ReceiveDtmf('1');
        time.Advance(TimeSpan.FromMilliseconds(300));
        call.ReceiveDtmf('2');
        time.Advance(TimeSpan.FromMilliseconds(300));
        call.ReceiveDtmf('2');

        var entry = await collecting;
        Assert.Equal("1122", entry.Digits);
    }

    [Fact]
    public async Task TheSubmitKey_EndsAShortEntry()
    {
        var (collector, call, time) = New();
        var collecting = collector.CollectAsync(Workspace, CallId, FourDigits);

        call.ReceiveDtmf('7');
        time.Advance(TimeSpan.FromMilliseconds(300));
        call.ReceiveDtmf('#');

        var entry = await collecting;
        Assert.Equal(DtmfEntryOutcome.Completed, entry.Outcome);
        Assert.Equal("7", entry.Digits);
    }

    [Fact]
    public async Task TheClearKey_EndsTheEntryAsCleared()
    {
        var (collector, call, time) = New();
        var collecting = collector.CollectAsync(Workspace, CallId, FourDigits);

        call.ReceiveDtmf('7');
        time.Advance(TimeSpan.FromMilliseconds(300));
        call.ReceiveDtmf('*');

        // Clearing hands control back: the consumer decides whether to replay the prompt and ask
        // again — that is policy, and policy does not live here.
        var entry = await collecting;
        Assert.Equal(DtmfEntryOutcome.Cleared, entry.Outcome);
        Assert.Null(entry.Digits);
    }

    [Fact]
    public async Task APauseLongerThanTheTimeout_EndsTheEntry()
    {
        var (collector, call, time) = New();
        var collecting = collector.CollectAsync(Workspace, CallId, FourDigits);

        call.ReceiveDtmf('1');
        time.Advance(TimeSpan.FromSeconds(6));

        // A silent line held open forever is worse than a lost attempt.
        var entry = await collecting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DtmfEntryOutcome.TimedOut, entry.Outcome);
    }

    [Fact]
    public async Task WhenTheCallEnds_TheEntryReportsItInsteadOfThrowing()
    {
        var (collector, call, _) = New();
        var collecting = collector.CollectAsync(Workspace, CallId, FourDigits);

        call.ReceiveDtmf('1');
        call.Transition(CallState.Terminated);

        // A caller who hangs up is not an error.
        var entry = await collecting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DtmfEntryOutcome.CallEnded, entry.Outcome);
    }

    [Fact]
    public async Task ASecondCollect_ReplacesTheFirstInsteadOfRunningBesideIt()
    {
        var (collector, call, _) = New();
        var first = collector.CollectAsync(Workspace, CallId, FourDigits);

        var second = collector.CollectAsync(Workspace, CallId, FourDigits);

        // Two collectors on one call would split the caller's digits between them.
        var firstEntry = await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DtmfEntryOutcome.Superseded, firstEntry.Outcome);
        Press(call, "1234");
        Assert.Equal("1234", (await second).Digits);
    }

    [Fact]
    public async Task Cancellation_EndsTheCollectionWithoutThrowing()
    {
        var (collector, _, _) = New();
        using var cts = new CancellationTokenSource();
        var collecting = collector.CollectAsync(Workspace, CallId, FourDigits, cts.Token);

        await cts.CancelAsync();

        var entry = await collecting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DtmfEntryOutcome.Superseded, entry.Outcome);
    }

    [Fact]
    public async Task ForACallOfAnotherWorkspace_ItFails()
    {
        var (collector, _, _) = New();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => collector.CollectAsync("ws-other", CallId, FourDigits));
    }

    private static (CallDtmfCollector Collector, ControllableCall Call, FakeTimeProvider Time) New()
    {
        var call = new ControllableCall(CallId, CallState.Connected);
        var time = new FakeTimeProvider();
        var collector = new CallDtmfCollector(new SingleCallAccess(Workspace, call), time);
        return (collector, call, time);
    }

    private static void Press(ControllableCall call, string digits)
    {
        foreach (var digit in digits)
        {
            call.ReceiveDtmf(digit);
        }
    }
}

/// <summary>Resolves one call for one workspace — the boundary the real service enforces.</summary>
internal sealed class SingleCallAccess(string workspaceKey, ICall call) : ICallAccess
{
    public ICall? Find(string ws, string callId) =>
        string.Equals(ws, workspaceKey, StringComparison.OrdinalIgnoreCase) && callId == call.CallId ? call : null;
}
