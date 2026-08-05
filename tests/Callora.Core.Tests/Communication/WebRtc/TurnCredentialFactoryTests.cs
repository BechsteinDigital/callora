using System.Security.Cryptography;
using System.Text;
using Callora.Plugin.Communication.Application.WebRtc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Callora.Core.Tests.Communication.WebRtc;

/// <summary>
/// TURN credentials handed to a browser have to expire (#114). The TURN REST API scheme derives them
/// from a shared secret the relay also holds, so a credential lifted from a browser is worthless once
/// its window passes and no long-lived password ever leaves the server.
/// </summary>
public sealed class TurnCredentialFactoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ASharedSecretYieldsACredentialTheRelayCanVerify()
    {
        var options = new IceConfigurationOptions(
            [new IceServerSetting("turn:turn.example.com:3478?transport=udp", SharedSecret: "s3cr3t")],
            TimeSpan.FromMinutes(10));

        var server = Assert.Single(TurnCredentialFactory.Build(options, "ws-a", Now));

        var expectedExpiry = Now.AddMinutes(10).ToUnixTimeSeconds();
        Assert.Equal($"{expectedExpiry}:ws-a", server.Username);
        Assert.Equal(Hmac("s3cr3t", server.Username!), server.Credential);
    }

    [Fact]
    public void TheCredentialWindowMovesWithTheClock()
    {
        var options = new IceConfigurationOptions(
            [new IceServerSetting("turn:turn.example.com:3478?transport=udp", SharedSecret: "s3cr3t")],
            TimeSpan.FromMinutes(10));

        var early = Assert.Single(TurnCredentialFactory.Build(options, "ws-a", Now));
        var late = Assert.Single(TurnCredentialFactory.Build(options, "ws-a", Now.AddMinutes(5)));

        Assert.NotEqual(early.Username, late.Username);
        Assert.NotEqual(early.Credential, late.Credential);
    }

    [Fact]
    public void AWorkspaceKeyContainingAColonCannotShiftTheExpiry()
    {
        // The colon separates expiry from identity; leaving one in would let the relay parse a
        // different deadline than the one that was signed.
        var options = new IceConfigurationOptions(
            [new IceServerSetting("turn:turn.example.com:3478?transport=udp", SharedSecret: "s3cr3t")],
            TimeSpan.FromMinutes(10));

        var server = Assert.Single(TurnCredentialFactory.Build(options, "ws:evil", Now));

        var expiry = Now.AddMinutes(10).ToUnixTimeSeconds();
        Assert.Equal($"{expiry}:ws-evil", server.Username);
    }

    [Fact]
    public void WithoutASharedSecret_TheConfiguredCredentialsArePassedThrough()
    {
        // An honest fallback: a deployment using static TURN credentials keeps working, and the
        // response says so by carrying no credential lifetime.
        var options = new IceConfigurationOptions(
            [new IceServerSetting("turn:turn.example.com:3478?transport=udp", Username: "user", Credential: "pass")],
            TimeSpan.FromMinutes(10));

        var server = Assert.Single(TurnCredentialFactory.Build(options, "ws-a", Now));

        Assert.Equal("user", server.Username);
        Assert.Equal("pass", server.Credential);
    }

    [Fact]
    public void AStunServerCarriesNoCredentials()
    {
        var options = new IceConfigurationOptions(
            [new IceServerSetting("stun:stun.example.com:3478")], TimeSpan.FromMinutes(10));

        var server = Assert.Single(TurnCredentialFactory.Build(options, "ws-a", Now));

        Assert.Null(server.Username);
        Assert.Null(server.Credential);
    }

    [Theory]
    [InlineData("stun", "udp", "stun:host.example:3478")]
    [InlineData("turn", "udp", "turn:host.example:3478?transport=udp")]
    [InlineData("turn", "tcp", "turn:host.example:3478?transport=tcp")]
    [InlineData("turn", "tls", "turns:host.example:3478?transport=tcp")]
    public void ConfiguredServersBecomeRfc7065Urls(string type, string transport, string expected)
    {
        var options = IceConfigurationOptions.FromConfiguration(Configuration(new Dictionary<string, string?>
        {
            ["WebRtc:IceServers:0:Host"] = "host.example",
            ["WebRtc:IceServers:0:Port"] = "3478",
            ["WebRtc:IceServers:0:Type"] = type,
            ["WebRtc:IceServers:0:Transport"] = transport,
        }));

        Assert.Equal(expected, Assert.Single(options.Servers).Url);
    }

    [Fact]
    public void AnUnconfiguredDeploymentOffersNoIceServers()
    {
        Assert.Empty(IceConfigurationOptions.FromConfiguration(null).Servers);
        Assert.Empty(IceConfigurationOptions.FromConfiguration(Configuration([])).Servers);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-30")]
    public void AnUnusableCredentialLifetime_FallsBackToTheDefault(string? configured)
    {
        var options = IceConfigurationOptions.FromConfiguration(Configuration(new Dictionary<string, string?>
        {
            ["WebRtc:CredentialTimeToLiveSeconds"] = configured,
        }));

        Assert.Equal(IceConfigurationOptions.DefaultCredentialTimeToLive, options.CredentialTimeToLive);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static string Hmac(string secret, string username) =>
#pragma warning disable CA5350 // Mirrors the TURN REST API the production code implements.
        Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(username)));
#pragma warning restore CA5350
}
