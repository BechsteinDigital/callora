namespace Callora.Core.Application.Surfaces.Layout;

/// <summary>One placed block: which block, where in the section, and how its controls are bound.</summary>
/// <param name="BlockId">The block's id — also its view id and its island attribute.</param>
/// <param name="Region">Region of the section layout it sits in.</param>
/// <param name="Position">Order within the region, ascending.</param>
/// <param name="Config">Control name → binding.</param>
public sealed record SurfaceLayoutBlock(
    string BlockId,
    string Region,
    int Position,
    IReadOnlyDictionary<string, SurfaceBlockBinding> Config);
