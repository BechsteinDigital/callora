using System.Collections.Generic;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk;
using CalloraVoipSdk.Core.Domain.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// H1 — deployment-tunable voice config. The <c>Communication:Voice</c> section drives transport and
/// media-security posture; absent/unparseable values fall back to the SDK defaults (so an unconfigured
/// deployment is unchanged). Codec and bridge format are always PCMU (the media bridge is µ-law only).
/// </summary>
public sealed class VoiceClientOptionsTests
{
    [Fact]
    public void FromConfiguration_NullOrEmpty_UsesDefaults()
    {
        var fromNull = VoiceClientOptions.FromConfiguration(null);
        var fromEmpty = VoiceClientOptions.FromConfiguration(new ConfigurationBuilder().Build());

        foreach (var options in new[] { fromNull, fromEmpty })
        {
            Assert.Equal(SipTransport.Udp, options.Transport);
            Assert.Equal(SrtpPolicy.Optional, options.SrtpPolicy);
            Assert.False(options.OfferDtlsSrtp);
            Assert.False(options.RequireSecureSignalingForSdes);
            Assert.Equal(System.TimeSpan.FromSeconds(15), options.InboundMediaTimeout);
        }
    }

    [Fact]
    public void FromConfiguration_ReadsAllValues()
    {
        var options = VoiceClientOptions.FromConfiguration(Config(new()
        {
            ["Communication:Voice:Transport"] = "Tls",
            ["Communication:Voice:SrtpPolicy"] = "Required",
            ["Communication:Voice:OfferDtlsSrtp"] = "true",
            ["Communication:Voice:RequireSecureSignalingForSdes"] = "true",
            ["Communication:Voice:InboundMediaTimeoutSeconds"] = "30",
        }));

        Assert.Equal(SipTransport.Tls, options.Transport);
        Assert.Equal(SrtpPolicy.Required, options.SrtpPolicy);
        Assert.True(options.OfferDtlsSrtp);
        Assert.True(options.RequireSecureSignalingForSdes);
        Assert.Equal(System.TimeSpan.FromSeconds(30), options.InboundMediaTimeout);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    public void FromConfiguration_UnparseableEnum_FallsBackToDefault(string transport)
    {
        var options = VoiceClientOptions.FromConfiguration(Config(new()
        {
            ["Communication:Voice:Transport"] = transport,
            ["Communication:Voice:InboundMediaTimeoutSeconds"] = "-5", // non-positive → default
        }));

        Assert.Equal(SipTransport.Udp, options.Transport);
        Assert.Equal(System.TimeSpan.FromSeconds(15), options.InboundMediaTimeout);
    }

    [Fact]
    public void BuildConfiguration_MapsOptions_AndKeepsPcmuBridge()
    {
        var options = new VoiceClientOptions
        {
            Transport = SipTransport.Tcp,
            SrtpPolicy = SrtpPolicy.Required,
            OfferDtlsSrtp = true,
            RequireSecureSignalingForSdes = true,
            InboundMediaTimeout = System.TimeSpan.FromSeconds(42),
        };

        var configuration = HeadlessVoipClientFactory.BuildConfiguration(options);

        Assert.Equal(SipTransport.Tcp, configuration.DefaultTransport);
        Assert.Equal(SrtpPolicy.Required, configuration.SrtpPolicy);
        Assert.True(configuration.OfferDtlsSrtp);
        Assert.True(configuration.RequireSecureSignalingForSdes);
        Assert.Equal(System.TimeSpan.FromSeconds(42), configuration.InboundMediaTimeout);
        // Fixed regardless of options: the media bridge is G.711 µ-law only.
        Assert.Equal(BridgeAudioFormat.Pcmu, configuration.BridgeAudioFormat);
        Assert.Equal(["PCMU"], configuration.PreferredAudioCodecs);
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
