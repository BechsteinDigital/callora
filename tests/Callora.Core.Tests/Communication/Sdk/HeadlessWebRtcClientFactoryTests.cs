using System.Collections.Generic;
using System.Net;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// WebRTC S1 — headless client setup. The plugin-scoped <c>WebRtc</c> section drives the deployment ICE
/// (STUN/TURN), audio codecs and video flag; absent/unparseable values fall back to the WebRTC standard
/// (Opus, host-only ICE). Unlike the SIP path there is no µ-law bridge — WebRTC is Opus/transport-only.
/// </summary>
public sealed class HeadlessWebRtcClientFactoryTests
{
    [Fact]
    public void BuildConfiguration_Defaults_OfferOpusAudioOnly()
    {
        var config = HeadlessWebRtcClientFactory.BuildConfiguration(new WebRtcClientOptions());

        Assert.Equal(["opus"], config.AudioCodecs);
        Assert.Equal(["H264"], config.VideoCodecs);
        Assert.False(config.EnableVideo);
        Assert.Empty(config.IceServers);
        Assert.Null(config.LoggerFactory);
    }

    [Fact]
    public void BuildConfiguration_MapsStunTurnAndIceServers_WithAllFields()
    {
        var options = new WebRtcClientOptions
        {
            AudioCodecs = ["opus", "PCMU"],
            VideoCodecs = ["VP8", "H264"],
            EnableVideo = true,
            LocalEndPoint = new IPEndPoint(IPAddress.Parse("10.0.0.5"), 40000),
            IceServers =
            [
                new IceServerConfiguration { Host = "stun.example.org", Port = 3478, Type = IceServerType.Stun, Transport = IceTransport.Udp },
                new IceServerConfiguration
                {
                    Host = "turn.example.org",
                    Port = 5349,
                    Type = IceServerType.Turn,
                    Transport = IceTransport.Tls,
                    Username = "user",
                    Password = "secret",
                },
            ],
        };

        var mapped = HeadlessWebRtcClientFactory.BuildConfiguration(options);

        Assert.Equal(["opus", "PCMU"], mapped.AudioCodecs);
        Assert.Equal(["VP8", "H264"], mapped.VideoCodecs);
        Assert.True(mapped.EnableVideo);
        Assert.Equal(new IPEndPoint(IPAddress.Parse("10.0.0.5"), 40000), mapped.LocalEndPoint);

        Assert.Collection(
            mapped.IceServers,
            stun =>
            {
                Assert.Equal("stun.example.org", stun.Host);
                Assert.Equal(3478, stun.Port);
                Assert.Equal(IceServerType.Stun, stun.Type);
                Assert.Equal(IceTransport.Udp, stun.Transport);
                Assert.Null(stun.Username);
            },
            turn =>
            {
                Assert.Equal("turn.example.org", turn.Host);
                Assert.Equal(5349, turn.Port);
                Assert.Equal(IceServerType.Turn, turn.Type);
                Assert.Equal(IceTransport.Tls, turn.Transport);
                Assert.Equal("user", turn.Username);
                Assert.Equal("secret", turn.Password);
            });
    }

    [Fact]
    public void FromConfiguration_NullOrEmpty_UsesDefaults()
    {
        var fromNull = WebRtcClientOptions.FromConfiguration(null);
        var fromEmpty = WebRtcClientOptions.FromConfiguration(new ConfigurationBuilder().Build());

        foreach (var options in new[] { fromNull, fromEmpty })
        {
            Assert.Equal(["opus"], options.AudioCodecs);
            Assert.Equal(["H264"], options.VideoCodecs);
            Assert.False(options.EnableVideo);
            Assert.Empty(options.IceServers);
        }
    }

    [Fact]
    public void FromConfiguration_ReadsIceServersCodecsAndVideo()
    {
        var options = WebRtcClientOptions.FromConfiguration(Config(new()
        {
            ["WebRtc:EnableVideo"] = "true",
            ["WebRtc:AudioCodecs:0"] = "opus",
            ["WebRtc:AudioCodecs:1"] = "G722",
            ["WebRtc:VideoCodecs:0"] = "VP8",
            ["WebRtc:VideoCodecs:1"] = "H264",
            ["WebRtc:IceServers:0:Host"] = "stun.example.org",
            ["WebRtc:IceServers:0:Type"] = "stun",
            ["WebRtc:IceServers:1:Host"] = "turn.example.org",
            ["WebRtc:IceServers:1:Port"] = "5349",
            ["WebRtc:IceServers:1:Type"] = "turn",
            ["WebRtc:IceServers:1:Transport"] = "tls",
            ["WebRtc:IceServers:1:Username"] = "user",
            ["WebRtc:IceServers:1:Password"] = "secret",
        }));

        Assert.True(options.EnableVideo);
        Assert.Equal(["opus", "G722"], options.AudioCodecs);
        Assert.Equal(["VP8", "H264"], options.VideoCodecs);

        Assert.Collection(
            options.IceServers,
            stun =>
            {
                Assert.Equal("stun.example.org", stun.Host);
                Assert.Equal(IceServerType.Stun, stun.Type);
                Assert.Null(stun.Port);
            },
            turn =>
            {
                Assert.Equal("turn.example.org", turn.Host);
                Assert.Equal(5349, turn.Port);
                Assert.Equal(IceServerType.Turn, turn.Type);
                Assert.Equal(IceTransport.Tls, turn.Transport);
                Assert.Equal("user", turn.Username);
                Assert.Equal("secret", turn.Password);
            });
    }

    [Fact]
    public void FromConfiguration_IceServerWithoutHost_IsSkipped()
    {
        var options = WebRtcClientOptions.FromConfiguration(Config(new()
        {
            ["WebRtc:IceServers:0:Type"] = "stun", // no Host → not a usable entry
            ["WebRtc:IceServers:1:Host"] = "stun.example.org",
        }));

        var server = Assert.Single(options.IceServers);
        Assert.Equal("stun.example.org", server.Host);
    }

    [Fact]
    public async Task Create_WithMinimalOptions_ReturnsUsableClient()
    {
        // Smoke: the headless factory builds a real client from defaults without a host DI container.
        // A client construction binds no socket (peers are created lazily), so this needs no network.
        await using var client = HeadlessWebRtcClientFactory.Create(new WebRtcClientOptions());

        Assert.NotNull(client);
        Assert.Empty(client.Peers.Active);
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
