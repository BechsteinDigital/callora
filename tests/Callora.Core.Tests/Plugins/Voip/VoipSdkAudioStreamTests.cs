using Callora.Core.Tests.Support;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Audio;
using Callora.Plugin.Communication.Application.Channels;
using CalloraVoipSdk.Core.Application.Media;
using Xunit;

namespace Callora.Core.Tests.Plugins.Voip;

public sealed class VoipSdkAudioStreamTests
{
    [Fact]
    public async Task Send_MapsDurationToRtpUnits_AndStampsPayloadType()
    {
        var receiver = new FakeMediaReceiver();
        var sender = new FakeMediaSender();
        await using var stream = new VoipSdkAudioStream(
            receiver, sender, new AudioFormat("PCMU", 8000), payloadType: 0);

        await stream.SendAsync(new AudioFrame(new byte[160], TimeSpan.FromMilliseconds(20)));

        var frame = Assert.Single(sender.SentFrames);
        Assert.Equal(0, frame.PayloadType);
        Assert.Equal(160u, frame.DurationRtpUnits);
    }

    [Fact]
    public async Task ReceivedFrames_MapRtpUnitsToDuration()
    {
        var receiver = new FakeMediaReceiver();
        var sender = new FakeMediaSender();
        await using var stream = new VoipSdkAudioStream(
            receiver, sender, new AudioFormat("G722", 16000), payloadType: 9);
        var receivedFrames = new List<AudioFrame>();
        stream.FrameReceived += (_, args) => receivedFrames.Add(args.Frame);

        receiver.RaiseFrame(new MediaFrame(new byte[320], PayloadType: 9, DurationRtpUnits: 320));

        var received = Assert.Single(receivedFrames);
        Assert.Equal(TimeSpan.FromMilliseconds(20), received.Duration);
        Assert.Equal(320, received.Payload.Length);
    }

    [Fact]
    public async Task Dispose_DetachesAndDisposesReceiverAndSender()
    {
        var receiver = new FakeMediaReceiver();
        var sender = new FakeMediaSender();
        var stream = new VoipSdkAudioStream(
            receiver, sender, new AudioFormat("PCMU", 8000), payloadType: 0);

        await stream.DisposeAsync();

        Assert.Null(receiver.AttachedCall);
        Assert.Null(sender.AttachedCall);
        Assert.True(receiver.IsDisposed);
        Assert.True(sender.IsDisposed);
    }

    [Fact]
    public async Task Dispose_StopsFrameDelivery()
    {
        var receiver = new FakeMediaReceiver();
        var sender = new FakeMediaSender();
        var stream = new VoipSdkAudioStream(
            receiver, sender, new AudioFormat("PCMU", 8000), payloadType: 0);
        var receivedCount = 0;
        stream.FrameReceived += (_, _) => receivedCount++;
        await stream.DisposeAsync();

        receiver.RaiseFrame(new MediaFrame(new byte[160], PayloadType: 0, DurationRtpUnits: 160));

        Assert.Equal(0, receivedCount);
    }
}
