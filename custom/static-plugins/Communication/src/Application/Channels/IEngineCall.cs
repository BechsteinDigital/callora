using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Audio;
using SdkCallDirection = CalloraVoipSdk.Core.Domain.Calls.CallDirection;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;

namespace Callora.Plugin.Communication.Application.Channels;

/// <summary>
/// Narrow plugin-internal port over one engine call. Keeps the platform
/// adapter testable without a live SIP stack.
/// </summary>
public interface IEngineCall
{
    string CallId { get; }

    SdkCallState State { get; }

    SdkCallDirection Direction { get; }

    /// <summary>Protocol address of the remote party, for example the caller URI.</summary>
    string RemoteParty { get; }

    /// <summary>Raised with the new engine state on every transition.</summary>
    event Action<SdkCallState>? StateChanged;

    /// <summary>Accepts an inbound ringing call.</summary>
    Task AcceptAsync(CancellationToken cancellationToken = default);

    /// <summary>Rejects an inbound ringing call with the engine default cause.</summary>
    Task RejectAsync(CancellationToken cancellationToken = default);

    Task HangupAsync(CancellationToken cancellationToken = default);

    Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default);

    /// <summary>Opens the bidirectional audio stream of the connected call.</summary>
    Task<ICallAudioStream> OpenAudioAsync(CancellationToken cancellationToken = default);
}
