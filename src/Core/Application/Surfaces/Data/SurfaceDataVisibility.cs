namespace Callora.Core.Application.Surfaces.Data;

/// <summary>
/// Whether a contribution depends on who is looking. The host enforces the consequences, so a
/// contributor states a fact about its data rather than remembering a rule.
/// </summary>
public enum SurfaceDataVisibility
{
    /// <summary>
    /// The same for every visitor — a product, a page of text, an opening-hours table. Safe on a
    /// Public surface and safe to cache.
    /// </summary>
    CallerIndependent = 0,

    /// <summary>
    /// Depends on the caller — their cart, their appointments, their case.
    /// <para>
    /// Two things follow, both enforced by the host rather than by the contributor: it is not
    /// invoked on a Public surface, where anyone who fetches the page would read it; and the
    /// response becomes uncacheable, because a proxy in front would otherwise serve the first
    /// visitor's data to everyone after them. That second one is the classic, quiet failure of
    /// this pattern.
    /// </para>
    /// </summary>
    CallerSpecific = 1,
}
