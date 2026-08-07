namespace Callora.Core.Application.Extensions;

/// <summary>
/// One section layout a theme offers, and the regions it contains.
/// <para>
/// Section layouts come from the theme, not from the core. A theme declares in
/// <c>theme.json</c> which layouts it can render (<c>single</c>, <c>two-2-1</c>,
/// <c>sidebar-left</c>, …) and which regions exist inside them; the editor offers exactly that
/// and nothing else. Two things follow. The token axis stays the design authority — nobody
/// composes a layout the theme cannot style. And no layout names end up in the core, so a theme
/// can bring a grid nobody anticipated without a change here.
/// </para>
/// </summary>
/// <param name="LayoutKey">
/// What the renderer writes into <c>data-cal-layout</c>, and what the theme's CSS selects on.
/// </param>
/// <param name="Label">What the editor shows.</param>
/// <param name="Regions">
/// Where blocks may go. Ordered as declared: the theme's order is the reading order, and an
/// alphabetical one would put a sidebar before the content it sits next to.
/// </param>
/// <param name="SortOrder">Ascending order in the editor's layout picker.</param>
public sealed record SectionLayoutDefinition(
    string LayoutKey,
    string Label,
    IReadOnlyList<SectionLayoutRegion> Regions,
    int SortOrder = 100);
