using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Tests.Communication.Admin;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Flows;
using Xunit;

namespace Callora.Core.Tests.Communication.Flows;

/// <summary>
/// Flow actions go through the call-control primitive rather than the provider's call object (#116).
/// That is what puts a rule under the same workspace ownership check, state machine and history
/// writes as an operator's click.
/// </summary>
public sealed class CallFlowActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheContributedActionTypesAreStable()
    {
        var service = new FakeCallControlService();

        Assert.Equal("call.accept", new CallAcceptActionHandler(service).Type);
        Assert.Equal("call.reject", new CallRejectActionHandler(service).Type);
        Assert.Equal("call.hangup", new CallHangupActionHandler(service).Type);
        Assert.Equal("call.dtmf", new SendDtmfActionHandler(service).Type);
    }

    [Fact]
    public async Task AcceptGoesThroughCallControl_ScopedToTheEventsWorkspace()
    {
        var service = new FakeCallControlService { ControlResult = true };

        await new CallAcceptActionHandler(service).ExecuteAsync(Context("ws-a", "call-1"), Parameters());

        Assert.Equal(("ws-a", "call-1"), service.LastAccepted);
    }

    [Fact]
    public async Task RejectGoesThroughCallControl()
    {
        var service = new FakeCallControlService { ControlResult = true };

        await new CallRejectActionHandler(service).ExecuteAsync(Context("ws-a", "call-1"), Parameters());

        Assert.Equal(("ws-a", "call-1"), service.LastRejected);
    }

    [Fact]
    public async Task HangupGoesThroughCallControl()
    {
        var service = new FakeCallControlService { HangupResult = true };

        await new CallHangupActionHandler(service).ExecuteAsync(Context("ws-a", "call-1"), Parameters());

        Assert.Equal(("ws-a", "call-1"), service.LastHangup);
    }

    [Fact]
    public async Task DtmfPassesItsConfiguredTones()
    {
        var service = new FakeCallControlService { ControlResult = true };

        await new SendDtmfActionHandler(service).ExecuteAsync(
            Context("ws-a", "call-1"), Parameters(("tones", "123#")));

        Assert.Equal(("ws-a", "call-1", "123#"), service.LastDtmf);
    }

    [Fact]
    public async Task DtmfWithoutConfiguredTones_IsAFlowAuthoringError()
    {
        var service = new FakeCallControlService { ControlResult = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SendDtmfActionHandler(service).ExecuteAsync(Context("ws-a", "call-1"), Parameters()));

        Assert.Null(service.LastDtmf);
    }

    [Fact]
    public async Task AnEventWithoutAWorkspace_IsRefused()
    {
        // Without a workspace there is no ownership to check, so there is no call this may touch.
        var service = new FakeCallControlService { ControlResult = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CallHangupActionHandler(service).ExecuteAsync(Context(null, "call-1"), Parameters()));

        Assert.Null(service.LastHangup);
    }

    [Fact]
    public async Task AnEventWithoutACallId_IsRefused()
    {
        var service = new FakeCallControlService { ControlResult = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CallHangupActionHandler(service).ExecuteAsync(Context("ws-a", callId: null), Parameters()));
    }

    [Fact]
    public async Task ACallThatEndedBeforeTheActionRan_IsReported()
    {
        // The normal race: the rule fired on a real event and the world moved on. A flow author
        // needs to see it rather than have it pass silently.
        var service = new FakeCallControlService { HangupResult = false };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CallHangupActionHandler(service).ExecuteAsync(Context("ws-a", "call-1"), Parameters()));

        Assert.Contains("call-1", error.Message, StringComparison.Ordinal);
    }

    private static RuleContext Context(string? workspaceKey, string? callId)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (callId is not null)
        {
            data["callId"] = callId;
        }

        return new RuleContext(CallEventTypes.Ringing, workspaceKey, data, Now);
    }

    private static IReadOnlyDictionary<string, string> Parameters(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
}
