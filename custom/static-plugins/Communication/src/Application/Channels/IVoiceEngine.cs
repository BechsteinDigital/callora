using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;

namespace Callora.Plugin.Communication.Application.Channels;

/// <summary>
/// Plugin-internal port over the telephony engine. The only implementation
/// touching CalloraVoipSdk is <see cref="VoipSdkVoiceEngine"/>.
/// </summary>
public interface IVoiceEngine : IAsyncDisposable
{
    /// <summary>
    /// Places one outbound call over the given SIP account.
    /// </summary>
    Task<IEngineCall> PlaceCallAsync(
        SipAccountEntry account,
        CallTarget target,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the SIP account and invokes the callback for every inbound
    /// call arriving on it. Disposing the returned handle ends the subscription.
    /// </summary>
    Task<IDisposable> SubscribeIncomingCallsAsync(
        SipAccountEntry account,
        Action<IEngineCall> onIncomingCall,
        CancellationToken cancellationToken = default);
}
