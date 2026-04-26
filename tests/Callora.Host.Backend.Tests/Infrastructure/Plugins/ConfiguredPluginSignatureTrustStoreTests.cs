using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Plugins;

namespace Callora.Host.Backend.Tests.Infrastructure.Plugins;

public sealed class ConfiguredPluginSignatureTrustStoreTests
{
    [Fact]
    public void IsTrusted_UnknownKey_ReturnsFalse()
    {
        var options = new BackendHostOptions
        {
            TrustedSignerThumbprints = ["AABBCCDD00112233445566778899AABBCCDDEEFF"]
        };

        var sut = new ConfiguredPluginSignatureTrustStore(options);

        var isTrusted = sut.IsTrusted("11223344556677889900AABBCCDDEEFF00112233");

        Assert.False(isTrusted);
    }

    [Fact]
    public void IsTrusted_ConfiguredKey_ReturnsTrue()
    {
        var options = new BackendHostOptions
        {
            TrustedSignerThumbprints = ["aabbccdd00112233445566778899aabbccddeeff"]
        };

        var sut = new ConfiguredPluginSignatureTrustStore(options);

        var isTrusted = sut.IsTrusted("AA BB CC DD 00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF");

        Assert.True(isTrusted);
    }

    [Fact]
    public void IsTrusted_StructuredSignerConfig_ReturnsTrue()
    {
        var options = new BackendHostOptions
        {
            TrustedSigners =
            [
                new BackendTrustedSignerOptions
                {
                    PublisherId = "acme-telephony",
                    DisplayName = "Acme Telephony GmbH",
                    Thumbprint = "11223344556677889900AABBCCDDEEFF00112233",
                    Source = "marketplace-sync"
                }
            ]
        };

        var sut = new ConfiguredPluginSignatureTrustStore(options);

        var isTrusted = sut.IsTrusted("11 22 33 44 55 66 77 88 99 00 aa bb cc dd ee ff 00 11 22 33");

        Assert.True(isTrusted);
    }

    [Fact]
    public void GetTrustedSigners_ReturnsNormalizedSignerEntries()
    {
        var options = new BackendHostOptions
        {
            TrustedSigners =
            [
                new BackendTrustedSignerOptions
                {
                    PublisherId = "acme-telephony",
                    DisplayName = "Acme Telephony GmbH",
                    Thumbprint = "11 22 33 44 55 66 77 88 99 00 aa bb cc dd ee ff 00 11 22 33",
                    Source = "marketplace-sync"
                }
            ]
        };

        var sut = new ConfiguredPluginSignatureTrustStore(options);

        var signers = sut.GetTrustedSigners();

        Assert.Single(signers);
        Assert.Equal("acme-telephony", signers[0].PublisherId);
        Assert.Equal("11223344556677889900AABBCCDDEEFF00112233", signers[0].Thumbprint);
        Assert.Equal("marketplace-sync", signers[0].Source);
    }
}
