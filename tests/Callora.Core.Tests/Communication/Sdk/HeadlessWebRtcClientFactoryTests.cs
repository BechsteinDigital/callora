using System.Collections.Generic;
using System.Net;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// WebRTC S1 — headless client setup. The <c>Communication:WebRtc</c> section drives the deployment ICE
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
            Assert.False(options.EnableVideo);
            Assert.Empty(options.IceServers);
        }
    }

    [Fact]
    public void FromConfiguration_ReadsIceServersCodecsAndVideo()
    {
        var options = WebRtcClientOptions.FromConfiguration(Config(new()
        {
            ["Communication:WebRtc:EnableVideo"] = "true",
            ["Communication:WebRtc:AudioCodecs:0"] = "opus",
            ["Communication:WebRtc:AudioCodecs:1"] = "G722",
            ["Communication:WebRtc:IceServers:0:Host"] = "stun.example.org",
            ["Communication:WebRtc:IceServers:0:Type"] = "stun",
            ["Communication:WebRtc:IceServers:1:Host"] = "turn.example.org",
            ["Communication:WebRtc:IceServers:1:Port"] = "5349",
            ["Communication:WebRtc:IceServers:1:Type"] = "turn",
            ["Communication:WebRtc:IceServers:1:Transport"] = "tls",
            ["Communication:WebRtc:IceServers:1:Username"] = "user",
            ["Communication:WebRtc:IceServers:1:Password"] = "secret",
        }));

        Assert.True(options.EnableVideo);
        Assert.Equal(["opus", "G722"], options.AudioCodecs);

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
            ["Communication:WebRtc:IceServers:0:Type"] = "stun", // no Host → not a usable entry
            ["Communication:WebRtc:IceServers:1:Host"] = "stun.example.org",
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
