namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Body of <c>POST calls/{callId}/dtmf</c>.
/// </summary>
/// <param name="Tones">
/// The tones to send, in order, for example <c>"123#"</c>. Accepts <c>0-9</c>, <c>*</c>, <c>#</c> and
/// <c>A-D</c>; anything else rejects the whole sequence.
/// </param>
public sealed record SendDtmfApiRequest(string? Tones);
