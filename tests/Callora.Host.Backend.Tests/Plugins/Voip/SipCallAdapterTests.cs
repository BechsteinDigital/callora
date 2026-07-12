using Callora.Contracts.Communication;
using Callora.Host.Backend.Tests.Support;
using Callora.Plugins.Voip.Application.Channels;
using Xunit;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;

namespace Callora.Host.Backend.Tests.Plugins.Voip;

public sealed class SipCallAdapterTests
{
    [Theory]
    [InlineData(SdkCallState.Idle, CallState.Connecting)]
    [InlineData(SdkCallState.Dialing, CallState.Connecting)]
    [InlineData(SdkCallState.Ringing, CallState.Ringing)]
    [InlineData(SdkCallState.Connected, CallState.Connected)]
    [InlineData(SdkCallState.OnHold, CallState.Connected)]
    [InlineData(SdkCallState.Transferring, CallState.Connected)]
    [InlineData(SdkCallState.Terminated, CallState.Terminated)]
    public void Mapper_MapsEngineStatesToPlatformStates(SdkCallState engineState, CallState expected)
    {
        Assert.Equal(expected, SipCallStateMapper.Map(engineState));
    }

    [Fact]
    public void StateChanges_PropagateMappedTransitions()
    {
        var engineCall = new FakeEngineCall(SdkCallState.Dialing);
        var call = new SipCall(engineCall, new CallTarget("+4930111"));
        var transitions = new List<(CallState Previous, CallState Current)>();
        call.StateChanged += (_, args) => transitions.Add((args.PreviousState, args.CurrentState));

        engineCall.RaiseState(SdkCallState.Ringing);
        engineCall.RaiseState(SdkCallState.Connected);
        engineCall.RaiseState(SdkCallState.Terminated);

        Assert.Equal(
            [
                (CallState.Connecting, CallState.Ringing),
                (CallState.Ringing, CallState.Connected),
                (CallState.Connected, CallState.Terminated)
            ],
            transitions);
        Assert.Equal(CallState.Terminated, call.State);
    }

    [Fact]
    public void EqualMappedStates_DoNotRaiseDuplicateTransitions()
    {
        var engineCall = new FakeEngineCall(SdkCallState.Connected);
        var call = new SipCall(engineCall, new CallTarget("+4930111"));
        var transitionCount = 0;
        call.StateChanged += (_, _) => transitionCount++;

        engineCall.RaiseState(SdkCallState.OnHold);
        engineCall.RaiseState(SdkCallState.Transferring);

        Assert.Equal(0, transitionCount);
        Assert.Equal(CallState.Connected, call.State);
    }

    [Fact]
    public async Task Hangup_DelegatesToEngineCall()
    {
        var engineCall = new FakeEngineCall(SdkCallState.Connected);
        var call = new SipCall(engineCall, new CallTarget("+4930111"));

        await call.HangupAsync();

        Assert.Equal(1, engineCall.HangupCallCount);
        Assert.Equal(CallState.Terminated, call.State);
    }

    [Fact]
    public async Task SendDtmf_DelegatesToEngineCall()
    {
        var engineCall = new FakeEngineCall(SdkCallState.Connected);
        var call = new SipCall(engineCall, new CallTarget("+4930111"));

        await call.SendDtmfAsync('5');
        await call.SendDtmfAsync('#');

        Assert.Equal(['5', '#'], engineCall.SentDtmfTones);
    }

    [Fact]
    public void NoTransitions_AfterTerminated()
    {
        var engineCall = new FakeEngineCall(SdkCallState.Connected);
        var call = new SipCall(engineCall, new CallTarget("+4930111"));
        engineCall.RaiseState(SdkCallState.Terminated);
        var transitionCount = 0;
        call.StateChanged += (_, _) => transitionCount++;

        engineCall.RaiseState(SdkCallState.Connected);

        Assert.Equal(0, transitionCount);
        Assert.Equal(CallState.Terminated, call.State);
    }
}
