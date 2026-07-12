using SdkCall = CalloraVoipSdk.Core.Domain.Calls.ICall;
using SdkCallDirection = CalloraVoipSdk.Core.Domain.Calls.CallDirection;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;
using SdkDtmfTone = CalloraVoipSdk.Core.Domain.Calls.DtmfTone;

namespace Callora.Plugins.Voip.Application.Channels;

/// <summary>
/// Wraps one CalloraVoipSdk call behind the narrow engine-call port.
/// </summary>
public sealed class VoipSdkEngineCall : IEngineCall
{
    private readonly SdkCall _inner;

    public VoipSdkEngineCall(SdkCall inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _inner.StateChanged += (_, args) => StateChanged?.Invoke(args.NewState);
    }

    public string CallId => _inner.CallId.ToString();

    public SdkCallState State => _inner.State;

    public SdkCallDirection Direction => _inner.Direction;

    public event Action<SdkCallState>? StateChanged;

    public Task HangupAsync(CancellationToken cancellationToken = default) =>
        _inner.HangupAsync(cancellationToken);

    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default) =>
        _inner.SendDtmfAsync(new SdkDtmfTone(tone), cancellationToken);
}
