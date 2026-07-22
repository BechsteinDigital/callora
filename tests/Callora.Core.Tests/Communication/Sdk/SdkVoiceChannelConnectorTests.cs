using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk.Core.Application.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using IPhoneLine = CalloraVoipSdk.Core.Domain.Lines.IPhoneLine;
using LineState = CalloraVoipSdk.Core.Domain.Lines.LineState;
using SdkSipAccount = CalloraVoipSdk.Core.Domain.Lines.SipAccount;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The real voice connector (B4-deep-2d-3b): a registered account becomes an <see cref="SdkVoiceChannel"/>
/// (identity from the account, health from the line, audio via the runtime's media tap); a
/// non-registering account yields null; unsupported auth surfaces from the account factory.
/// The untestable SIP round-trip is stubbed behind the <see cref="ISdkVoiceRuntime"/> seam.
/// </summary>
public sealed class SdkVoiceChannelConnectorTests
{
    private const string PluginId = "communication";

    [Fact]
    public async Task ConnectAsync_RegisteredAccount_ReturnsVoiceChannel()
    {
        var runtime = new FakeSdkVoiceRuntime { NextLine = new FakePhoneLine { State = LineState.Registered } };
        var connector = NewConnector(runtime);

        var channel = await connector.ConnectAsync(DigestAccount());

        Assert.NotNull(channel);
        Assert.Equal("acc-1", channel!.ChannelId);
        Assert.Equal("Alice Line", channel.DisplayName);
        Assert.Equal(PluginId, channel.PluginId);
        Assert.Equal(ChannelHealth.Up, channel.Health); // from LineState.Registered
    }

    [Fact]
    public async Task ConnectAsync_AccountDoesNotRegister_ReturnsNull()
    {
        var runtime = new FakeSdkVoiceRuntime { NextLine = null };
        var connector = NewConnector(runtime);

        Assert.Null(await connector.ConnectAsync(DigestAccount()));
    }

    [Fact]
    public async Task ConnectAsync_ProducedChannel_OpensAudioViaRuntimeTap()
    {
        var line = new FakePhoneLine { State = LineState.Registered, DialResult = new FakeSdkCall() };
        var runtime = new FakeSdkVoiceRuntime { NextLine = line };
        var connector = NewConnector(runtime);

        var channel = Assert.IsAssignableFrom<IVoiceChannel>(await connector.ConnectAsync(DigestAccount()));
        var call = await channel.PlaceCallAsync(new CallTarget("sip:bob@example.com"));
        await using var audio = await ((IVoipCall)call).OpenAudioAsync();

        Assert.Equal(1, runtime.MediaTapCount); // the channel's audio tap came from the runtime
    }

    [Fact]
    public async Task ConnectAsync_UnsupportedAuth_SurfacesFromFactory()
    {
        var connector = NewConnector(new FakeSdkVoiceRuntime { NextLine = new FakePhoneLine() });
        var connection = new SipConnection(
            "sip.example.com", 5060, SipTransport.Udp, SipAccountMode.Trunk, IpAuthentication.Instance, registrationExpirySeconds: null);
        var account = new SipAccount("acc-ip", "w1", "IP Trunk", connection, maxConcurrentCalls: 1, enabled: true);

        await Assert.ThrowsAsync<System.NotSupportedException>(() => connector.ConnectAsync(account));
    }

    private static SdkVoiceChannelConnector NewConnector(ISdkVoiceRuntime runtime) =>
        new(
            new SdkSipAccountFactory(new FakePluginDataProtector(("pw-ref", "s3cret")), PluginId),
            runtime,
            PluginId,
            NullLogger<SdkVoiceChannelConnector>.Instance);

    private static SipAccount DigestAccount()
    {
        var auth = new DigestAuthentication("alice", authId: null, passwordSecretRef: "pw-ref");
        var connection = new SipConnection("sip.example.com", 5060, SipTransport.Udp, SipAccountMode.Register, auth, 600);
        return new SipAccount("acc-1", "w1", "Alice Line", connection, maxConcurrentCalls: 2, enabled: true);
    }
}

/// <summary>A minimal <see cref="ISdkVoiceRuntime"/> double: a canned line and a counted media tap.</summary>
internal sealed class FakeSdkVoiceRuntime : ISdkVoiceRuntime
{
    public IPhoneLine? NextLine { get; set; }

    public int MediaTapCount { get; private set; }

    public Task<IPhoneLine?> ConnectAsync(SdkSipAccount account, CancellationToken cancellationToken = default) =>
        Task.FromResult(NextLine);

    public (IMediaReceiver Receiver, IMediaSender Sender) CreateMediaTap()
    {
        MediaTapCount++;
        return (new FakeMediaReceiver(), new FakeMediaSender());
    }
}
