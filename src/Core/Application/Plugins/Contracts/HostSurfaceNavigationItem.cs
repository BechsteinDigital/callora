namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One navigation entry a plugin contributes to a surface (#125 block C), the
/// counterpart of <see cref="HostAdminNavigationItem"/> for the visitor-facing side.
/// <para>
/// It carries meaning, not presentation. Whether the theme renders these as a sidebar,
/// tabs, a launcher or a menu is the theme's decision, which is why there is no width,
/// no placement and no markup here.
/// </para>
/// </summary>
/// <param name="Id">Stable id, unique per plugin.</param>
/// <param name="Label">Text the theme displays.</param>
/// <param name="To">Target the entry points at, relative to the surface root.</param>
/// <param name="Icon">Optional icon key the theme may render.</param>
/// <param name="Order">Ascending order; equal values keep declaration order.</param>
/// <param name="SurfaceKeys">
/// Optional allowlist of surface keys. Empty means the entry is contributed
/// workspace-wide.
/// </param>
/// <param name="RequiredClaims">
/// Optional claim keys the caller must carry. Matched on presence only and filtered on
/// the server, so an entry a visitor may not use is not merely greyed out.
/// </param>
public sealed record HostSurfaceNavigationItem(
    string Id,
    string Label,
    string To,
    string? Icon = null,
    int Order = 0,
    IReadOnlyList<string>? SurfaceKeys = null,
    IReadOnlyList<string>? RequiredClaims = null);
