namespace Callora.Core.Application.Surfaces.Layout;

/// <summary>
/// One section of a layout.
/// <para>
/// <paramref name="Layout"/> names a section layout the THEME declares (single, two-2-1,
/// sidebar-left …), not one the core knows. That keeps the token axis the design authority and
/// keeps layout names out of the core — a theme that offers a new arrangement needs no change here.
/// </para>
/// <para>
/// <paramref name="Spacing"/> and <paramref name="SurfaceRole"/> are token STEPS, never values.
/// That is the guardrail: a section can be roomier or quieter, but it cannot be 37 pixels or
/// #ff00ff.
/// </para>
/// </summary>
public sealed record SurfaceLayoutSection(
    string Layout,
    int Position,
    IReadOnlyList<SurfaceLayoutBlock> Blocks,
    string? Spacing = null,
    string? SurfaceRole = null);
