using Callora.Plugin.Communication.Abstractions;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;

namespace Callora.Plugins.Voip.Application.Channels;

/// <summary>
/// Maps SIP engine call states onto the coarser platform call states.
/// </summary>
public static class SipCallStateMapper
{
    public static CallState Map(SdkCallState engineState) =>
        engineState switch
        {
            SdkCallState.Idle => CallState.Connecting,
            SdkCallState.Dialing => CallState.Connecting,
            SdkCallState.Ringing => CallState.Ringing,
            SdkCallState.Connected => CallState.Connected,
            SdkCallState.OnHold => CallState.Connected,
            SdkCallState.Transferring => CallState.Connected,
            SdkCallState.Terminated => CallState.Terminated,
            _ => CallState.Terminated,
        };
}
