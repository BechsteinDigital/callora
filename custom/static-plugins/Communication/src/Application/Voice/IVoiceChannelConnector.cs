using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Voice;

/// <summary>
/// Connects one persisted <see cref="SipAccount"/> to its live voice runtime and returns the ready
/// <see cref="IVoiceChannel"/>, or <see langword="null"/> when the account could not register/connect.
/// This is the seam that isolates the real SDK client (network, SIP registration) behind a
/// fake-testable, SDK-free port: the provisioning logic depends only on this contract.
/// </summary>
public interface IVoiceChannelConnector
{
    /// <summary>
    /// Connects <paramref name="account"/> and returns its live channel, or <see langword="null"/>
    /// when registration/connection failed (the implementation logs the reason).
    /// </summary>
    Task<IVoiceChannel?> ConnectAsync(SipAccount account, CancellationToken cancellationToken = default);
}
