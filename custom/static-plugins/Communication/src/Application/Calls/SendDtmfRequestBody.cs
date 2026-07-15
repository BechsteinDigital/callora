namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>Body of POST /api/calls/{callId}/dtmf.</summary>
public sealed record SendDtmfRequestBody(string Tone);
