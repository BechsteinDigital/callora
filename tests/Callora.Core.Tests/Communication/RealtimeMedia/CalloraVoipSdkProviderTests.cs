using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Callora.Plugin.Communication.Infrastructure.RealtimeMedia;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk;
using Xunit;

namespace Callora.Core.Tests.Communication.RealtimeMedia;

/// <summary>
/// The CalloraVoipSdk provider adapter: <c>CreatePeer</c> mints a neutral peer over the SDK client's peer,
/// disposal cascades to the client, and the neutral <see cref="MediaPeerOptions"/> map onto the SDK
/// <c>WebRtcConfiguration</c> (a pure, client-free mapping).
/// </summary>
public sealed class CalloraVoipSdkProviderTests
{
    private static readonly MediaPeerOptions Options = new();

    [Fact]
    public void CreatePeer_DelegatesToClientAndWrapsPeer()
    {
        var client = new FakeSdkWebRtcClient();
        var provider = new CalloraVoipSdkProvider(client);

        var peer = provider.CreatePeer(Options);

        Assert.Equal(1, client.CreatePeerCallCount);
        Assert.IsType<CalloraVoipSdkMediaPeer>(peer);
    }

    [Fact]
    public async Task DisposeAsync_DisposesUnderlyingClient()
    {
        var client = new FakeSdkWebRtcClient();
        var provider = new CalloraVoipSdkProvider(client);

        await provider.DisposeAsync();

        Assert.True(client.DisposeAsyncCalled);
    }

    [Fact]
    public void BuildConfiguration_MapsNeutralOptionsOntoSdk()
    {
        var options = new MediaPeerOptions
        {
            AudioCodecs = ["opus", "PCMU"],
            VideoCodecs = ["H264"],
            EnableVideo = true,
            UseStableNumericMediaIds = true,
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 5555),
            IceServers =
            [
                new MediaIceServer("turn.example.org", 3478, "turn", "tls", "user", "secret"),
            ],
        };

        var config = CalloraVoipSdkProvider.BuildConfiguration(options);

        Assert.Equal(["opus", "PCMU"], config.AudioCodecs);
        Assert.Equal(["H264"], config.VideoCodecs);
        Assert.True(config.EnableVideo);
        Assert.True(config.UseStableNumericMediaIds);
        Assert.Equal(new IPEndPoint(IPAddress.Loopback, 5555), config.LocalEndPoint);

        var mapped = Assert.Single(config.IceServers);
        Assert.Equal("turn.example.org", mapped.Host);
        Assert.Equal(3478, mapped.Port);
        Assert.Equal(IceServerType.Turn, mapped.Type);
        Assert.Equal(IceTransport.Tls, mapped.Transport);
        Assert.Equal("user", mapped.Username);
        Assert.Equal("secret", mapped.Password);
    }

    [Fact]
    public void BuildConfiguration_DefaultsAreNeutralAndSafe()
    {
        var config = CalloraVoipSdkProvider.BuildConfiguration(new MediaPeerOptions());

        Assert.Equal(["opus"], config.AudioCodecs);
        Assert.False(config.EnableVideo);
        Assert.Empty(config.IceServers);
    }

    [Fact]
    public void ConferencePeerOptionsEnableStableNumericMediaIdsForRenegotiation()
    {
        var options = CommunicationPlugin.ToConferencePeerOptions(new WebRtcClientOptions());

        Assert.True(options.UseStableNumericMediaIds);
        Assert.Equal(["H264"], options.VideoCodecs);
    }
}
