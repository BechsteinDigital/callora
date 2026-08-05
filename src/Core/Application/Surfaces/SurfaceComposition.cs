namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Everything the plugins compose into one surface render (#125 block C): what fills
/// each slot, and what belongs in the navigation. Resolved together because both run
/// the same filters over the same contributors, and a caller that may not see a view
/// should not see its navigation entry either.
/// </summary>
/// <param name="Slots">Views per slot, filtered and ordered.</param>
/// <param name="Navigation">Navigation entries, filtered and ordered.</param>
public sealed record SurfaceComposition(
    IReadOnlyDictionary<string, IReadOnlyList<SurfaceSlotView>> Slots,
    IReadOnlyList<SurfaceNavigationEntry> Navigation)
{
    /// <summary>A surface with nothing composed into it.</summary>
    public static SurfaceComposition Empty { get; } = new(
        new Dictionary<string, IReadOnlyList<SurfaceSlotView>>(StringComparer.Ordinal),
        []);
}
