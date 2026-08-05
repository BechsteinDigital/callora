namespace Callora.Surface.Rendering;

/// <summary>
/// The allowlisted data a surface template may read (ADR-015 §8). Only these
/// values become script variables in the sandbox — a template never sees a .NET
/// type or reflection surface. Extended as later phases add profile/identity.
/// </summary>
public sealed record SurfaceRenderContext(
    string TenantKey,
    string WorkspaceKey,
    string SurfaceKey,
    string SurfaceType,
    string Locale,
    IReadOnlyDictionary<string, string> Tokens)
{
    /// <summary>
    /// Who is looking at this page (ADR-017 §9) — a guest or an authenticated
    /// visitor. Null only in a composition without the identity subsystem, so a
    /// minimal host keeps rendering. It never carries the session token: a template
    /// may read the caller, but must not be able to pass their session on.
    /// </summary>
    public SurfaceCallerView? Caller { get; init; }
}
