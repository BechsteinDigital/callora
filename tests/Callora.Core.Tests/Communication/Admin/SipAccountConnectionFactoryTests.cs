using System;
using System.Collections.Generic;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Application.Admin.SipAccounts;
using Callora.Plugin.Communication.Domain.Accounts;
using Xunit;

namespace Callora.Core.Tests.Communication.Admin;

/// <summary>
/// The shared create/update connection builder: it defaults the registration expiry from the auth
/// type (registers unless Trunk + IP auth), threads the trunk inbound fields through, and turns
/// invalid combinations into a message rather than an exception.
/// </summary>
public sealed class SipAccountConnectionFactoryTests
{
    private const string PluginId = "communication";

    [Fact]
    public void TryBuild_CredentialedTrunk_DefaultsExpiryAndKeepsTrunkFields()
    {
        var factory = new SipAccountConnectionFactory(new PassthroughDataProtector(), PluginId);
        var input = new FakeInput
        {
            Host = "trunk.example.com",
            AuthMethod = SipAuthMethod.Digest,
            Mode = SipAccountMode.Trunk,
            Username = "trunk-user",
            Password = "s3cret",
            OutboundProxy = "proxy.example.com",
            InboundNumbers = ["+4930111", "+4930222"],
        };

        var built = factory.TryBuild(input, existing: null, out var connection, out var error);

        Assert.True(built, error);
        Assert.NotNull(connection);
        Assert.Equal(SipAccountMode.Trunk, connection!.Mode);
        Assert.Equal(300, connection.RegistrationExpirySeconds); // registers → default expiry
        Assert.Equal("proxy.example.com", connection.OutboundProxy);
        Assert.Equal(["+4930111", "+4930222"], connection.InboundNumbers);
    }

    [Fact]
    public void TryBuild_IpAuthenticatedTrunk_HasNoExpiryAndEmptyInboundNumbers()
    {
        var factory = new SipAccountConnectionFactory(new PassthroughDataProtector(), PluginId);
        var input = new FakeInput
        {
            Host = "trunk.example.com",
            AuthMethod = SipAuthMethod.IpAuthenticated,
            // Mode omitted → defaults to Trunk for IP auth.
        };

        var built = factory.TryBuild(input, existing: null, out var connection, out var error);

        Assert.True(built, error);
        Assert.Equal(SipAccountMode.Trunk, connection!.Mode);
        Assert.Null(connection.RegistrationExpirySeconds); // registration-less
        Assert.Empty(connection.InboundNumbers);
    }

    [Fact]
    public void TryBuild_RegisterWithIpAuth_ReturnsErrorNotException()
    {
        var factory = new SipAccountConnectionFactory(new PassthroughDataProtector(), PluginId);
        var input = new FakeInput
        {
            Host = "sip.example.com",
            AuthMethod = SipAuthMethod.IpAuthenticated,
            Mode = SipAccountMode.Register, // invalid combination
        };

        var built = factory.TryBuild(input, existing: null, out var connection, out var error);

        Assert.False(built);
        Assert.Null(connection);
        Assert.NotNull(error);
    }

    private sealed class FakeInput : ISipConnectionInput
    {
        public string? Host { get; init; }
        public int? Port { get; init; }
        public SipTransport? Transport { get; init; }
        public SipAuthMethod? AuthMethod { get; init; }
        public SipAccountMode? Mode { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
        public string? AuthId { get; init; }
        public string? ClientCertificate { get; init; }
        public string? ClientCertificateSecretRef { get; init; }
        public int? RegistrationExpirySeconds { get; init; }
        public string? OutboundProxy { get; init; }
        public IReadOnlyList<string>? InboundNumbers { get; init; }
    }

    /// <summary>A data protector that returns the plaintext as its own reference (round-trips 1:1).</summary>
    private sealed class PassthroughDataProtector : IPluginDataProtector
    {
        public string Protect(string pluginId, string plaintext) => $"protected:{plaintext}";

        public bool TryUnprotect(string pluginId, string protectedValue, out string plaintext)
        {
            plaintext = protectedValue;
            return true;
        }
    }
}
