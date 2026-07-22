using System;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The callId → live-stream registry backing the WS media surface (B4-deep-1): register/open/remove
/// with workspace-agnostic call ids (a call id is globally unique).
/// </summary>
public sealed class SdkCallAudioStreamProviderTests
{
    [Fact]
    public async Task Register_ThenOpen_ReturnsTheStream()
    {
        var provider = new SdkCallAudioStreamProvider();
        var stream = new StubAudioStream();

        provider.Register("call-1", stream);

        Assert.Same(stream, await provider.OpenAsync("call-1"));
    }

    [Fact]
    public async Task Open_UnknownCallId_ReturnsNull()
    {
        var provider = new SdkCallAudioStreamProvider();

        Assert.Null(await provider.OpenAsync("missing"));
    }

    [Fact]
    public async Task Remove_ReturnsTheStream_AndOpenThenNull()
    {
        var provider = new SdkCallAudioStreamProvider();
        var stream = new StubAudioStream();
        provider.Register("call-1", stream);

        Assert.Same(stream, provider.Remove("call-1"));
        Assert.Null(provider.Remove("call-1"));
        Assert.Null(await provider.OpenAsync("call-1"));
    }
}

internal sealed class StubAudioStream : ICallAudioStream
{
    public AudioFormat Format => AudioFormat.G711Ulaw8k20ms;

#pragma warning disable CS0067 // Registry stub: the provider never raises this event.
    public event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;
#pragma warning restore CS0067

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
