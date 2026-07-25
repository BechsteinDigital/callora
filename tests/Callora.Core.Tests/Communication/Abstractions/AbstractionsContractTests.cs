using Callora.Plugin.Communication.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Abstractions;

/// <summary>
/// Contract-Tests der neu entworfenen Communication-Abstractions (B1): die Verträge sind
/// implementierbar und in beiden Richtungen nutzbar. Echtes Impl-Verhalten folgt in B3+.
/// </summary>
public sealed class AbstractionsContractTests
{
    [Fact]
    public void AudioFormat_G711Default_HasSipPstnValues()
    {
        var format = AudioFormat.G711Ulaw8k20ms;

        Assert.Equal(AudioCodec.G711Ulaw, format.Codec);
        Assert.Equal(8000, format.SampleRateHz);
        Assert.Equal(20, format.FrameMilliseconds);
    }

    [Fact]
    public async Task CallAudioStream_DuplexContract_RoundTripsInboundAndOutbound()
    {
        await using var stream = new FakeCallAudioStream();
        var received = new List<byte[]>();
        stream.FrameReceived += (_, args) => received.Add(args.Frame.ToArray());

        stream.EmitInbound(new byte[] { 1, 2, 3 });
        await stream.SendAsync(new byte[] { 4, 5, 6 });

        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(received));
        Assert.Equal(new byte[] { 4, 5, 6 }, Assert.Single(stream.Sent));
        Assert.Equal(AudioFormat.G711Ulaw8k20ms, stream.Format);
    }

    [Fact]
    public void VoiceChannel_IsCommunicationChannel_AndExposesHealth()
    {
        ICommunicationChannel channel = new FakeVoiceChannel(ChannelHealth.Up);

        Assert.Equal(ChannelHealth.Up, channel.Health);
        Assert.Contains(CommunicationCapabilities.Voice, channel.Capabilities);
    }

    private sealed class FakeCallAudioStream : ICallAudioStream
    {
        public List<byte[]> Sent { get; } = [];

        public AudioFormat Format => AudioFormat.G711Ulaw8k20ms;

        public event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

        public void EmitInbound(byte[] frame) =>
            FrameReceived?.Invoke(this, new AudioFrameReceivedEventArgs(frame));

        public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
        {
            Sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeVoiceChannel(ChannelHealth health) : IVoiceChannel
    {
        public string ChannelId => "fake";
        public string DisplayName => "Fake Voice";
        public string PluginId => "fake";
        public IReadOnlyCollection<string> Capabilities { get; } = [CommunicationCapabilities.Voice];
        public ChannelHealth Health { get; } = health;

#pragma warning disable CS0067 // Vertrags-Fake: Events gehören zur Schnittstelle, werden hier nicht ausgelöst.
        public event EventHandler<ChannelHealthChangedEventArgs>? HealthChanged;
        public event EventHandler<IncomingCallEventArgs>? IncomingCall;
#pragma warning restore CS0067

        public Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Contract-Fake ohne Outbound.");
    }
}
