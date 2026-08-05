using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// One view the host decided a slot actually holds for this request (#125 block C):
/// already filtered by surface, caller claims and plugin availability, and already
/// ordered. A template renders what it is given and re-decides nothing.
/// </summary>
/// <param name="ViewId">Island id the browser runtime mounts the component into.</param>
/// <param name="PluginId">Plugin that contributed the view.</param>
/// <param name="Slot">Semantic role the view fills.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="Weight">Ascending order within the slot.</param>
/// <param name="Cardinality">Whether the slot may hold several instances of this view.</param>
/// <param name="Icon">Optional icon key the theme may render.</param>
/// <param name="ProvidesContexts">Namespaced context keys the view publishes.</param>
/// <param name="RequiresContexts">Namespaced context keys the view consumes.</param>
public sealed record SurfaceSlotView(
    string ViewId,
    string PluginId,
    string Slot,
    string DisplayName,
    int Weight,
    SurfaceViewCardinality Cardinality,
    string? Icon,
    IReadOnlyList<string> ProvidesContexts,
    IReadOnlyList<string> RequiresContexts);
