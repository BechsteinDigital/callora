using CalloraVoipSdk.Core.Application.Media;
using CalloraVoipSdk.Core.Domain.Lines;
using SdkSipAccount = CalloraVoipSdk.Core.Domain.Lines.SipAccount;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// The narrow SDK boundary the voice connector depends on: register an SDK account to a live line,
/// and create a per-call media tap. Isolating exactly these two operations keeps the untestable SIP
/// registration / media wiring of the real CalloraVoipSdk client out of the connector, so the
/// connector's branching and channel construction stay fake-testable.
/// </summary>
public interface ISdkVoiceRuntime
{
    /// <summary>
    /// Registers <paramref name="account"/> and returns its live line, or <see langword="null"/> when
    /// registration did not succeed.
    /// </summary>
    Task<IPhoneLine?> ConnectAsync(SdkSipAccount account, CancellationToken cancellationToken = default);

    /// <summary>Creates a fresh inbound/outbound media tap pair for one call.</summary>
    (IMediaReceiver Receiver, IMediaSender Sender) CreateMediaTap();
}
