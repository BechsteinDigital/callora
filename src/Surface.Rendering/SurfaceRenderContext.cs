using Callora.Core.Application.Surfaces;

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

    /// <summary>
    /// What each surface slot holds for this request, keyed by slot name (#125 block C).
    /// Already filtered by surface, caller claims and plugin availability, and already
    /// ordered, so a template renders what it is given and re-decides nothing. Read
    /// through <c>callora_slot()</c> rather than directly in most templates.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SurfaceSlotView>> Slots { get; init; } =
        new Dictionary<string, IReadOnlyList<SurfaceSlotView>>(StringComparer.Ordinal);

    /// <summary>
    /// Navigation entries the plugins contributed for this caller (#125 block C),
    /// filtered and ordered by the host. Meaning only: whether the theme renders them
    /// as a sidebar, tabs, a launcher or a menu is the theme's decision.
    /// </summary>
    public IReadOnlyList<SurfaceNavigationEntry> Navigation { get; init; } = [];

    /// <summary>
    /// The composed layout, already rendered to islands, or null when no layout is published for
    /// this surface. Read through <c>callora_composition()</c> rather than interpolated: it is
    /// markup, and a template that interpolated it would ship it escaped.
    /// </summary>
    public string? CompositionHtml { get; init; }
}
