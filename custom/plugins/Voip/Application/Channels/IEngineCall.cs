using SdkCallDirection = CalloraVoipSdk.Core.Domain.Calls.CallDirection;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;

namespace Callora.Plugins.Voip.Application.Channels;

/// <summary>
/// Narrow plugin-internal port over one engine call. Keeps the platform
/// adapter testable without a live SIP stack.
/// </summary>
public interface IEngineCall
{
    string CallId { get; }

    SdkCallState State { get; }

    SdkCallDirection Direction { get; }

    /// <summary>Raised with the new engine state on every transition.</summary>
    event Action<SdkCallState>? StateChanged;

    Task HangupAsync(CancellationToken cancellationToken = default);

    Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default);
}
