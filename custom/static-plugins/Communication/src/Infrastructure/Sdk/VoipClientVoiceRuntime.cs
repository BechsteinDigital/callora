using CalloraVoipSdk;
using CalloraVoipSdk.Core.Application.Media;
using CalloraVoipSdk.Core.Domain.Lines;
using SdkSipAccount = CalloraVoipSdk.Core.Domain.Lines.SipAccount;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// The real <see cref="ISdkVoiceRuntime"/> over a CalloraVoipSdk <see cref="IVoipClient"/>. Deliberately
/// thin: the two operations delegate straight to the SDK. The actual SIP registration and media wiring
/// can only be exercised against a real registrar (validated end-to-end in B4-deep-3), so this class
/// carries no unit tests — all decision logic lives in <see cref="SdkVoiceChannelConnector"/>.
/// </summary>
public sealed class VoipClientVoiceRuntime : ISdkVoiceRuntime
{
    private readonly IVoipClient _client;

    /// <summary>Wraps the SDK voice client the plugin owns.</summary>
    public VoipClientVoiceRuntime(IVoipClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    public async Task<IPhoneLine?> ConnectAsync(SdkSipAccount account, CancellationToken cancellationToken = default)
    {
        var result = await _client.ConnectAsync(account, ConnectOptions.Default, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? result.Line : null;
    }

    /// <inheritdoc />
    public (IMediaReceiver Receiver, IMediaSender Sender) CreateMediaTap() =>
        (_client.Media.CreateReceiver(), _client.Media.CreateSender());
}
