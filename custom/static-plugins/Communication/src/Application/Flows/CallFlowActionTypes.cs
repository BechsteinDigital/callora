namespace Callora.Plugin.Communication.Application.Flows;

/// <summary>Action type keys the Communication plugin contributes to flows.</summary>
public static class CallFlowActionTypes
{
    /// <summary>Answers the ringing inbound call the event names.</summary>
    public const string Accept = "call.accept";

    /// <summary>Turns away the ringing inbound call the event names.</summary>
    public const string Reject = "call.reject";

    /// <summary>Ends the call the event names.</summary>
    public const string Hangup = "call.hangup";

    /// <summary>Sends keypad tones on the call the event names.</summary>
    public const string SendDtmf = "call.dtmf";
}
