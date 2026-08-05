namespace Callora.Surface.Rendering.Api;

/// <summary>
/// Asks for a handoff ticket to another surface of the same workspace
/// (ADR-017 §8.4).
/// </summary>
/// <param name="SurfaceKey">Target surface the visitor is being sent to.</param>
/// <param name="ReturnPath">
/// Where to land on the target surface after redemption. Must be a site-relative
/// path; anything else is replaced by the target's root, because a caller-supplied
/// absolute URL would turn the redeem endpoint into an open redirect.
/// </param>
public sealed record SurfaceHandoffTicketApiRequest(string? SurfaceKey, string? ReturnPath);
