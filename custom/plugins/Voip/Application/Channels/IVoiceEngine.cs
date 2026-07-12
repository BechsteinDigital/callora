using Callora.Contracts.Communication;
using Callora.Plugins.Voip.Application.Accounts;

namespace Callora.Plugins.Voip.Application.Channels;

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
}
