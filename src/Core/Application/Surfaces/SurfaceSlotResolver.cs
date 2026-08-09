using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;

using Callora.Core.Application.Workspaces;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Decides what the plugins compose into one surface render (#125 block C): what fills
/// each slot, and what belongs in the navigation.
/// <para>
/// Every filter runs here, on the server, before any markup exists. A view a visitor
/// may not see is not emitted at all rather than hidden by CSS, and the order a theme
/// renders is the order the host decided, so it cannot depend on which plugin bundle
/// happened to load first.
/// </para>
/// </summary>
public sealed class SurfaceSlotResolver(
    ICalloraPluginCatalog pluginCatalog,
    IPluginAvailabilityEvaluator availabilityEvaluator)
{
    /// <summary>
    /// Resolves slots and navigation for a surface and caller.
    /// </summary>
    /// <param name="workspaceKey">Workspace the surface belongs to.</param>
    /// <param name="surfaceKey">Surface being rendered.</param>
    /// <param name="caller">Who is looking at the page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceComposition> ResolveAsync(
        string workspaceKey,
        string surfaceKey,
        SurfaceCaller caller,
        // Was diese Fläche jedem Besucher gewährt (ADR-023). Durchgereicht statt hier geholt:
        // Der Aufrufer hat die effektive Sicht der Fläche bereits in der Hand.
        string? grantedClaims = null,
        // Wer auf dieser Fläche überhaupt beitragen darf — die UI-Kette. Null heißt: alle, wie
        // bisher; ein Host ohne Kettenauflösung soll nicht plötzlich leer rendern.
        IReadOnlyCollection<string>? chain = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceKey);
        ArgumentNullException.ThrowIfNull(caller);

        var claims = SurfaceVisibility.ClaimsOn(caller, grantedClaims);
        var bySlot = new Dictionary<string, List<SurfaceSlotView>>(StringComparer.Ordinal);
        var navigation = new List<SurfaceNavigationEntry>();
        var availability = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        // Zwei Stellen entschieden, wer beiträgt: die Kette (was geladen wird) und diese
        // Schleife (was gerendert wird). Ein Plugin, das keine Flächen nennt, erschien deshalb
        // ÜBERALL — die Videokonferenz mit ihrer Navigation auf jeder Inhaltsfläche, auch auf
        // einer ohne einen einzigen Block. Jetzt entscheidet die Kette, und diese Schleife folgt.
        // null heißt „keine Angabe" — ein Host ohne Kettenauflösung soll nicht plötzlich leer
        // rendern. Eine LEERE Kette heißt „ausdrücklich niemand", und das ist der häufigste
        // Fall: eine Inhaltsfläche ohne einen einzigen Block. Beides gleichzusetzen hieß, dass
        // genau dort wieder jedes Plugin durchkam — der Fehler, den die Kürzung beheben sollte.
        var allowed = chain is null
            ? null
            : new HashSet<string>(chain, StringComparer.OrdinalIgnoreCase);

        foreach (var export in pluginCatalog.GetOwnedExports(typeof(IHostSurfaceViewContributor)))
        {
            if (export.Service is not IHostSurfaceViewContributor contributor)
            {
                continue;
            }

            var pluginId = string.IsNullOrWhiteSpace(contributor.PluginId)
                ? export.PluginId
                : contributor.PluginId;

            if (allowed is not null && !allowed.Contains(pluginId))
            {
                continue;
            }
            if (!await IsAvailableAsync(availability, pluginId, workspaceKey, cancellationToken)
                    .ConfigureAwait(false))
            {
                continue;
            }

            foreach (var view in contributor.Views ?? [])
            {
                if (!IsEligible(view, surfaceKey, claims))
                {
                    continue;
                }

                var slot = view.Slot.Trim();
                if (!bySlot.TryGetValue(slot, out var views))
                {
                    views = [];
                    bySlot[slot] = views;
                }

                views.Add(new SurfaceSlotView(
                    view.ViewId.Trim(),
                    pluginId,
                    slot,
                    view.DisplayName,
                    view.Weight,
                    view.Cardinality,
                    view.Icon,
                    view.ProvidesContexts ?? [],
                    view.RequiresContexts ?? []));
            }

            foreach (var item in contributor.NavigationItems ?? [])
            {
                if (IsEligible(item, surfaceKey, claims))
                {
                    navigation.Add(new SurfaceNavigationEntry(
                        item.Id, pluginId, item.Label, item.To, item.Icon, item.Order));
                }
            }
        }

        return new SurfaceComposition(
            Finalize(bySlot),
            navigation.OrderBy(static entry => entry.Order).ToArray());
    }

    private static bool IsEligible(
        HostSurfaceNavigationItem item,
        string surfaceKey,
        IReadOnlySet<string> claims)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Label))
        {
            return false;
        }

        if (item.SurfaceKeys is { Count: > 0 } &&
            !item.SurfaceKeys.Contains(surfaceKey, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return item.RequiredClaims is not { Count: > 0 } || item.RequiredClaims.All(claims.Contains);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<SurfaceSlotView>> Finalize(
        Dictionary<string, List<SurfaceSlotView>> bySlot)
    {
        var resolved = new Dictionary<string, IReadOnlyList<SurfaceSlotView>>(StringComparer.Ordinal);
        foreach (var (slot, views) in bySlot)
        {
            // Stable within equal weights: OrderBy is a stable sort, so declaration
            // order decides only where weight does not. One island per view id, so a
            // view declared Single cannot end up mounted twice into the same slot.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            resolved[slot] = views
                .OrderBy(static view => view.Weight)
                .Where(view => seen.Add(view.ViewId))
                .ToArray();
        }

        return resolved;
    }

    private static bool IsEligible(
        HostSurfaceViewRegistration view,
        string surfaceKey,
        IReadOnlySet<string> claims)
    {
        if (string.IsNullOrWhiteSpace(view.ViewId) || string.IsNullOrWhiteSpace(view.Slot))
        {
            return false;
        }

        if (view.SurfaceKeys is { Count: > 0 } &&
            !view.SurfaceKeys.Contains(surfaceKey, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Presence only. What a claim means belongs to the plugin that issued it, so
        // the host never compares values and never grants anything on their strength.
        return view.RequiredClaims is not { Count: > 0 } ||
               view.RequiredClaims.All(claims.Contains);
    }

    private async Task<bool> IsAvailableAsync(
        Dictionary<string, bool> cache,
        string pluginId,
        string workspaceKey,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(pluginId, out var cached))
        {
            return cached;
        }

        var availability = await availabilityEvaluator
            .EvaluateAsync(pluginId, workspaceKey, cancellationToken)
            .ConfigureAwait(false);
        cache[pluginId] = availability.IsAvailable;
        return availability.IsAvailable;
    }

}
