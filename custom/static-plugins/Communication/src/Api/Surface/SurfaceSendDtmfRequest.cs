namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>Body of <c>POST calls/{callId}/dtmf</c>.</summary>
/// <param name="Tones">The keys pressed, for example <c>12#</c>.</param>
public sealed record SurfaceSendDtmfRequest(string? Tones);
