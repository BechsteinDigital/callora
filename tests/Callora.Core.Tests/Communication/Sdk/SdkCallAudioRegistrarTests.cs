using System.Threading.Tasks;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The call → audio-provider lifecycle bridge (B4-deep-2c): a tracked call registers its audio stream
/// on Connected and has it removed and disposed on Terminated, so the WebSocket media surface can
/// resolve live call audio by call id. Exercised through the real SdkCall/SdkCallAudioStream stack.
/// </summary>
public sealed class SdkCallAudioRegistrarTests
{
    [Fact]
    public async Task Track_OnConnected_RegistersAudioStream()
    {
        var provider = new SdkCallAudioStreamProvider();
        var registrar = new SdkCallAudioRegistrar(provider, NullLogger<SdkCallAudioRegistrar>.Instance);
        var (call, sdk, _) = NewCall(SdkCallState.Ringing);

        registrar.Track(call);
        sdk.RaiseStateChanged(SdkCallState.Ringing, SdkCallState.Connected);

        Assert.NotNull(await provider.OpenAsync(call.CallId));
    }

    [Fact]
    public async Task Track_OnTerminated_RemovesAndDisposesStream()
    {
        var provider = new SdkCallAudioStreamProvider();
        var registrar = new SdkCallAudioRegistrar(provider, NullLogger<SdkCallAudioRegistrar>.Instance);
        var (call, sdk, receiver) = NewCall(SdkCallState.Ringing);
        registrar.Track(call);
        sdk.RaiseStateChanged(SdkCallState.Ringing, SdkCallState.Connected);

        sdk.RaiseStateChanged(SdkCallState.Connected, SdkCallState.Terminated);

        Assert.Null(await provider.OpenAsync(call.CallId)); // removed
        Assert.True(receiver.Disposed); // stream torn down
    }

    [Fact]
    public async Task Track_AlreadyConnected_RegistersImmediately()
    {
        var provider = new SdkCallAudioStreamProvider();
        var registrar = new SdkCallAudioRegistrar(provider, NullLogger<SdkCallAudioRegistrar>.Instance);
        var (call, _, _) = NewCall(SdkCallState.Connected);

        registrar.Track(call);

        Assert.NotNull(await provider.OpenAsync(call.CallId));
    }

    [Fact]
    public async Task Track_AlreadyTerminated_IsIgnored()
    {
        var provider = new SdkCallAudioStreamProvider();
        var registrar = new SdkCallAudioRegistrar(provider, NullLogger<SdkCallAudioRegistrar>.Instance);
        var (call, _, _) = NewCall(SdkCallState.Terminated);

        registrar.Track(call);

        Assert.Null(await provider.OpenAsync(call.CallId));
    }

    [Fact]
    public async Task Track_IsIdempotent_SingleStreamPerCall()
    {
        var provider = new SdkCallAudioStreamProvider();
        var registrar = new SdkCallAudioRegistrar(provider, NullLogger<SdkCallAudioRegistrar>.Instance);
        var (call, sdk, receiver) = NewCall(SdkCallState.Ringing);

        registrar.Track(call);
        registrar.Track(call); // second track must not double-subscribe
        sdk.RaiseStateChanged(SdkCallState.Ringing, SdkCallState.Connected);
        sdk.RaiseStateChanged(SdkCallState.Connected, SdkCallState.Terminated);

        Assert.Equal(1, receiver.DisposeCount); // exactly one stream opened and disposed
        Assert.Null(await provider.OpenAsync(call.CallId));
    }

    [Fact]
    public async Task ClearAsync_DisposesAllTrackedStreams_AndUnsubscribes()
    {
        var provider = new SdkCallAudioStreamProvider();
        var registrar = new SdkCallAudioRegistrar(provider, NullLogger<SdkCallAudioRegistrar>.Instance);
        var (callA, sdkA, receiverA) = NewCall(SdkCallState.Connected);
        var (callB, sdkB, receiverB) = NewCall(SdkCallState.Connected);
        registrar.Track(callA);
        registrar.Track(callB);

        await registrar.ClearAsync();

        Assert.True(receiverA.Disposed);
        Assert.True(receiverB.Disposed);
        Assert.Null(await provider.OpenAsync(callA.CallId));
        Assert.Null(await provider.OpenAsync(callB.CallId));

        // Unsubscribed: a post-clear terminate must not throw or touch the provider.
        sdkA.RaiseStateChanged(SdkCallState.Connected, SdkCallState.Terminated);
        Assert.Equal(1, receiverA.DisposeCount);
    }

    private static (SdkCall Call, FakeSdkCall Sdk, FakeMediaReceiver Receiver) NewCall(SdkCallState initial)
    {
        var sdk = new FakeSdkCall { State = initial };
        var receiver = new FakeMediaReceiver();
        var call = new SdkCall(sdk, () => (receiver, new FakeMediaSender()));
        return (call, sdk, receiver);
    }
}
