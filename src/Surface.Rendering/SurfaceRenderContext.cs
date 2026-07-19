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
    IReadOnlyDictionary<string, string> Tokens);
