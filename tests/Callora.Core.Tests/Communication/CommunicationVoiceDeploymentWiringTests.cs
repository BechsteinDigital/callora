using System;
using System.Collections.Generic;
using System.Linq;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Callora.Core.Tests.Communication.Sdk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Callora.Core.Tests.Communication;

/// <summary>
/// Deployment wiring of the self-built voice client (B4-deep-3 follow-up): voice turns on when the host
/// injects an <see cref="ISdkVoiceRuntime"/> (tests/custom hosts) or when configuration sets
/// plugin-scoped <c>Voice:Enabled=true</c>. An injected runtime always wins; otherwise the plugin builds
/// its own only when configured. The self-build → real-registration path itself is covered end-to-end by
/// the opt-in Asterisk integration tests.
/// </summary>
public sealed class CommunicationVoiceDeploymentWiringTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData("not-a-bool", false)]
    public void IsVoiceEnabled_ReadsConfigFlag(string value, bool expected)
    {
        var services = new StubServiceProvider((typeof(IConfiguration), BuildConfig(value)));

        Assert.Equal(expected, CommunicationPlugin.IsVoiceEnabled(services));
    }

    [Fact]
    public void IsVoiceEnabled_KeyAbsent_IsFalse()
    {
        var services = new StubServiceProvider((typeof(IConfiguration),
            new ConfigurationBuilder().Build()));

        Assert.False(CommunicationPlugin.IsVoiceEnabled(services));
    }

    [Fact]
    public void IsVoiceEnabled_NoConfigurationRegistered_IsFalse()
    {
        Assert.False(CommunicationPlugin.IsVoiceEnabled(new StubServiceProvider()));
    }

    [Fact]
    public void ResolveVoiceRuntime_InjectedRuntime_WinsOverConfig()
    {
        var injected = new FakeSdkVoiceRuntime();
        var services = new StubServiceProvider(
            (typeof(ISdkVoiceRuntime), injected),
            (typeof(IConfiguration), BuildConfig("true")));

        // The injected runtime is used verbatim — the plugin does not build (and would not own) a client.
        Assert.Same(injected, new CommunicationPlugin().ResolveVoiceRuntime(services));
    }

    [Fact]
    public void ResolveVoiceRuntime_NoInjection_VoiceDisabled_ReturnsNull()
    {
        var services = new StubServiceProvider((typeof(IConfiguration), BuildConfig("false")));

        Assert.Null(new CommunicationPlugin().ResolveVoiceRuntime(services));
    }

    private static IConfiguration BuildConfig(string enabledValue) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CommunicationPlugin.VoiceEnabledConfigKey] = enabledValue,
            })
            .Build();

    private sealed class StubServiceProvider(params (Type Type, object? Instance)[] services) : IServiceProvider
    {
        private readonly Dictionary<Type, object?> _services =
            services.ToDictionary(s => s.Type, s => s.Instance);

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var instance) ? instance : null;
    }
}
