using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk.Core.Application.Media;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The SDK↔foundation audio adapter (B4-deep-1): inbound SDK frames become copied
/// <c>FrameReceived</c> events, outbound frames forward as µ-law <c>MediaFrame</c>s (PT 0, 160
/// units), and dispose unsubscribes/detaches/disposes the underlying receiver and sender.
/// </summary>
public sealed class SdkCallAudioStreamTests
{
    [Fact]
    public void Inbound_SdkFrame_BecomesFrameReceived_WithACopy()
    {
        var receiver = new FakeMediaReceiver();
        var stream = new SdkCallAudioStream(receiver, new FakeMediaSender());
        byte[]? received = null;
        stream.FrameReceived += (_, e) => received = e.Frame.ToArray();

        var source = new byte[] { 1, 2, 3 };
        receiver.RaiseFrame(new MediaFrame(source, PayloadType: 0, DurationRtpUnits: 160));
        source[0] = 99; // mutate the source after the callback

        Assert.Equal(new byte[] { 1, 2, 3 }, received); // adapter copied — unaffected by the mutation
    }

    [Fact]
    public async Task Outbound_SendAsync_ForwardsMuLawFrame()
    {
        var sender = new FakeMediaSender();
        var stream = new SdkCallAudioStream(new FakeMediaReceiver(), sender);

        await stream.SendAsync(new byte[] { 5, 6, 7 });

        var sent = Assert.Single(sender.Sent);
        Assert.Equal(new byte[] { 5, 6, 7 }, sent.Payload.ToArray());
        Assert.Equal(0, sent.PayloadType);
        Assert.Equal(160u, sent.DurationRtpUnits);
    }

    [Fact]
    public void Format_IsG711Ulaw8k20ms()
    {
        var stream = new SdkCallAudioStream(new FakeMediaReceiver(), new FakeMediaSender());

        Assert.Equal(AudioFormat.G711Ulaw8k20ms, stream.Format);
    }

    [Fact]
    public async Task DisposeAsync_Unsubscribes_Detaches_Disposes()
    {
        var receiver = new FakeMediaReceiver();
        var sender = new FakeMediaSender();
        var stream = new SdkCallAudioStream(receiver, sender);
        byte[]? received = null;
        stream.FrameReceived += (_, e) => received = e.Frame.ToArray();

        await stream.DisposeAsync();
        receiver.RaiseFrame(new MediaFrame(new byte[] { 1 }, 0, 160)); // after dispose

        Assert.Null(received); // unsubscribed
        Assert.True(receiver.Detached);
        Assert.True(receiver.Disposed);
        Assert.True(sender.Detached);
        Assert.True(sender.Disposed);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var receiver = new FakeMediaReceiver();
        var sender = new FakeMediaSender();
        var stream = new SdkCallAudioStream(receiver, sender);

        await stream.DisposeAsync();
        await stream.DisposeAsync();

        Assert.Equal(1, receiver.DisposeCount);
        Assert.Equal(1, sender.DisposeCount);
    }
}

internal sealed class FakeMediaReceiver : IMediaReceiver
{
    public event EventHandler<MediaFrameReceivedEventArgs>? FrameReceived;

    public bool Detached { get; private set; }

    public int DisposeCount { get; private set; }

    public bool Disposed => DisposeCount > 0;

    /// <summary>The call this receiver was last attached to (null until <see cref="AttachToCall"/>).</summary>
    public CalloraVoipSdk.Core.Domain.Calls.ICall? AttachedCall { get; private set; }

    public void RaiseFrame(MediaFrame frame) => FrameReceived?.Invoke(this, new MediaFrameReceivedEventArgs(frame));

    public void AttachToCall(CalloraVoipSdk.Core.Domain.Calls.ICall call) => AttachedCall = call;

    public void Detach() => Detached = true;

    public void Dispose() => DisposeCount++;
}

internal sealed class FakeMediaSender : IMediaSender
{
    public List<MediaFrame> Sent { get; } = [];

    public bool Detached { get; private set; }

    public int DisposeCount { get; private set; }

    public bool Disposed => DisposeCount > 0;

    /// <summary>The call this sender was last attached to (null until <see cref="AttachToCall"/>).</summary>
    public CalloraVoipSdk.Core.Domain.Calls.ICall? AttachedCall { get; private set; }

    public Task SendAsync(MediaFrame frame, CancellationToken ct = default)
    {
        Sent.Add(frame);
        return Task.CompletedTask;
    }

    public void AttachToCall(CalloraVoipSdk.Core.Domain.Calls.ICall call) => AttachedCall = call;

    public void Detach() => Detached = true;

    public void Dispose() => DisposeCount++;
}
