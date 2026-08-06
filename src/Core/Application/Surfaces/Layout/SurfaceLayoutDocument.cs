namespace Callora.Core.Application.Surfaces.Layout;

/// <summary>
/// What a surface renders, as one immutable document.
/// <para>
/// A version is a snapshot and is never partially changed. Rolling back is copying a row, a diff
/// is trivial, and the renderer reads one document instead of three joins. Shopware normalises AND
/// versions at every level (cmsPageVersionId, cmsSectionVersionId, cmsBlockVersionId); that
/// machinery buys a query — "which layouts use block X" — that a narrow derived index answers just
/// as well.
/// </para>
/// </summary>
/// <param name="Key">Identity of the layout this is a version of.</param>
/// <param name="VersionNumber">Ascending; only publishing creates one.</param>
/// <param name="Sections">In render order.</param>
public sealed record SurfaceLayoutDocument(
    string Key,
    int VersionNumber,
    IReadOnlyList<SurfaceLayoutSection> Sections);
