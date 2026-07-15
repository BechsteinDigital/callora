namespace Callora.Core.Api;

/// <summary>
/// Request body for sending one DTMF tone on an active call.
/// </summary>
public sealed record SendCallDtmfRequest(string Tone);
