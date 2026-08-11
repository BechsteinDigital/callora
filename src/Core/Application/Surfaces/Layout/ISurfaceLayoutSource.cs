using Callora.Core.Extensibility;

namespace Callora.Core.Application.Surfaces.Layout;

/// <summary>
/// Where a composed layout comes from. The core defines exactly this contract; the composer plugin
/// implements it and owns the data, and the composition renderer asks it. No composer installed →
/// no layout → the surface renders from <c>.njk</c> as before.
/// <para>
/// <b>Two methods, not one with a flag.</b> The public render path calls only
/// <see cref="GetPublishedAsync"/>. There is no <c>?preview=true</c>, no header, no parameter with
/// which a draft could be requested from outside — on a Public surface such a hole would sit
/// behind no authentication at all. A flag would put both behind one call and make the guarantee a
/// matter of remembering to pass false.
/// </para>
/// </summary>
[CalloraExtensible("Extension point — implement to supply composed surface layouts")]
public interface ISurfaceLayoutSource
{
    /// <summary>
    /// The published layout for a surface, or null when none is composed. The only method the
    /// public render path may call.
    /// </summary>
    Task<SurfaceLayoutDocument?> GetPublishedAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Die Surface-Schlüssel dieses Workspaces, für die eine Erlebniswelt veröffentlicht ist.
    /// <para>
    /// Eine Abfrage für alle, nicht eine je Knoten: Die Navigation zeigt bei jedem Aufruf den
    /// ganzen Baum, und einzeln gefragt wären das so viele Datenbankrunden wie Knoten — auf
    /// dem öffentlichen Renderpfad, bei jedem Besucher.
    /// </para>
    /// </summary>
    Task<IReadOnlySet<string>> ListPublishedSurfaceKeysAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The working draft, for the editor. Requires operator permission at its call site — the
    /// public render path must never reach this.
    /// <para>
    /// <b>The workspace key is a parameter, not the caller's job.</b> A layout key IS a surface
    /// key, and surfaces are called <c>kontakt</c>, <c>impressum</c>, <c>startseite</c> — two
    /// tenants picking the same name is the normal case, which is why a composed layout is keyed
    /// by <c>(workspace, key)</c>. Without the workspace an implementation cannot decide WHICH
    /// draft it owes and returns whichever row it finds first.
    /// </para>
    /// <para>
    /// This method had no caller in the core when the parameter was added, and that was the
    /// argument FOR adding it: the first caller would have inherited the missing scope. In the
    /// composer that had already happened once — four route handlers took the layout key from the
    /// path and passed it on unfiltered, so an operator could read and publish another tenant's
    /// layouts. The test that was supposed to cover it asserted <c>route.Scope</c>, a metadatum,
    /// and stayed green throughout.
    /// </para>
    /// </summary>
    Task<SurfaceLayoutDocument?> GetDraftAsync(
        string workspaceKey,
        string layoutKey,
        CancellationToken cancellationToken = default);
}
