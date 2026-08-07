namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>Body of <c>POST calls</c> from a surface dialer.</summary>
/// <param name="To">Who to call.</param>
/// <param name="DisplayName">Optional name to show the other side, where the line supports it.</param>
/// <remarks>
/// No origin and no channel. The origin is what a quota is keyed by, and a browser that could name
/// its own would evade every limit by renaming itself — a plugin runs trusted in-process (ADR-013),
/// a page does not. The channel is left to the workspace's own choice for the same reason: picking a
/// line is an operator's decision, not a visitor's.
/// </remarks>
public sealed record SurfacePlaceCallRequest(string? To, string? DisplayName = null);
