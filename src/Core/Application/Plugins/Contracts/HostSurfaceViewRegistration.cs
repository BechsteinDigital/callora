namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One composable view a plugin contributes to a surface slot (#125 block C).
/// <para>
/// A view declares the semantic role it fills, not the place it occupies. The theme
/// decides where a role appears, in what order and inside which markup, so a workspace
/// with five plugins composes into one workplace instead of five separate applications.
/// </para>
/// </summary>
/// <param name="ViewId">
/// Stable id, also the value of <c>data-callora-island</c>. The plugin's browser
/// bundle registers its Vue component under the same id.
/// </param>
/// <param name="Slot">
/// Semantic role this view fills, for example <c>workspace.main</c> or
/// <c>lead.detail.panel</c>. Callora does not interpret the name; it only matches it
/// against what a template asks for.
/// </param>
/// <param name="DisplayName">Human-readable name for admin and a later layout editor.</param>
/// <param name="Weight">Ascending order within the slot; equal weights keep declaration order.</param>
/// <param name="Cardinality">Whether the slot may hold several instances of this view.</param>
/// <param name="Description">Optional longer description for the same audience.</param>
/// <param name="Icon">Optional icon key the theme may render.</param>
/// <param name="SurfaceKeys">
/// Optional allowlist of surface keys. Empty means the view is contributed
/// workspace-wide, which is the normal case for a workplace block.
/// </param>
/// <param name="RequiredClaims">
/// Optional claim keys the caller must carry for the view to be emitted at all. The
/// host matches on presence only and never interprets a value: what a claim means
/// stays with the plugin that issued it. Server-side filtering, so a view a visitor
/// may not see is not merely hidden in the browser.
/// </param>
/// <param name="ProvidesContexts">
/// Namespaced context keys this view publishes, for example <c>crm.lead-selection/v1</c>.
/// </param>
/// <param name="RequiresContexts">Namespaced context keys this view needs to be useful.</param>
public sealed record HostSurfaceViewRegistration(
    string ViewId,
    string Slot,
    string DisplayName,
    int Weight = 0,
    SurfaceViewCardinality Cardinality = SurfaceViewCardinality.Multiple,
    string? Description = null,
    string? Icon = null,
    IReadOnlyList<string>? SurfaceKeys = null,
    IReadOnlyList<string>? RequiredClaims = null,
    IReadOnlyList<string>? ProvidesContexts = null,
    IReadOnlyList<string>? RequiresContexts = null);
