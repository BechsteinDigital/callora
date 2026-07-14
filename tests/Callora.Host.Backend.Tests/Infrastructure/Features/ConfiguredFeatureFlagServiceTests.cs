using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Features;

namespace Callora.Host.Backend.Tests.Infrastructure.Features;

public sealed class ConfiguredFeatureFlagServiceTests
{
    [Fact]
    public void IsEnabled_ReflectsConfiguredValue()
    {
        var service = new ConfiguredFeatureFlagService(new BackendHostOptions
        {
            FeatureFlags = new() { ["voicemail"] = true, ["beta-dialer"] = false }
        });

        Assert.True(service.IsEnabled("voicemail"));
        Assert.False(service.IsEnabled("beta-dialer"));
    }

    [Fact]
    public void IsEnabled_UnknownOrBlankFlag_IsFalse()
    {
        var service = new ConfiguredFeatureFlagService(new BackendHostOptions());

        Assert.False(service.IsEnabled("does-not-exist"));
        Assert.False(service.IsEnabled(""));
        Assert.False(service.IsEnabled("   "));
    }

    [Fact]
    public void IsEnabled_IsCaseInsensitive()
    {
        var service = new ConfiguredFeatureFlagService(new BackendHostOptions
        {
            FeatureFlags = new() { ["Voicemail"] = true }
        });

        Assert.True(service.IsEnabled("voicemail"));
        Assert.True(service.IsEnabled("VOICEMAIL"));
    }

    [Fact]
    public void GetAll_ReturnsEveryDefinedFlag()
    {
        var service = new ConfiguredFeatureFlagService(new BackendHostOptions
        {
            FeatureFlags = new() { ["a"] = true, ["b"] = false }
        });

        Assert.Equal(2, service.GetAll().Count);
    }
}
