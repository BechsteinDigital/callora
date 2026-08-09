using Callora.Core.Application.Extensions;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Surfaces.Layout;
using Callora.Core.Application.Workspaces;

namespace Callora.Core.Application.Extensions;

/// <summary>
/// Resolves the ordered plugin UI load chain for one workspace: template
/// plugins first, in effective template resolution order, followed by the
/// workspace's remaining active plugins. The shells load UI bundles in this
/// order so later bundles can extend blocks contributed by earlier ones.
/// </summary>
public sealed class WorkspaceUiChainResolver
{
    private readonly IWorkspaceTemplateResolutionService _templateResolution;
    private readonly IWorkspacePluginActivationReader _activationReader;
    private readonly IPluginAvailabilityEvaluator _availabilityEvaluator;
    private readonly IWorkspaceSurfaceStore? _surfaceStore;
    private readonly ISurfaceLayoutSource? _layouts;

    public WorkspaceUiChainResolver(
        IWorkspaceTemplateResolutionService templateResolution,
        IWorkspacePluginActivationReader activationReader,
        IPluginAvailabilityEvaluator availabilityEvaluator)
        : this(templateResolution, activationReader, availabilityEvaluator, surfaceStore: null)
    {
    }

    public WorkspaceUiChainResolver(
        IWorkspaceTemplateResolutionService templateResolution,
        IWorkspacePluginActivationReader activationReader,
        IPluginAvailabilityEvaluator availabilityEvaluator,
        IWorkspaceSurfaceStore? surfaceStore)
        : this(templateResolution, activationReader, availabilityEvaluator, surfaceStore, layouts: null)
    {
    }

    /// <param name="layouts">
    /// Die veröffentlichten Layouts, oder null. Ohne sie bleibt es für eine Fläche ohne App bei
    /// allen aktiven Plugins — ein Host ohne Composer soll nicht plötzlich leer rendern.
    /// </param>
    public WorkspaceUiChainResolver(
        IWorkspaceTemplateResolutionService templateResolution,
        IWorkspacePluginActivationReader activationReader,
        IPluginAvailabilityEvaluator availabilityEvaluator,
        IWorkspaceSurfaceStore? surfaceStore,
        ISurfaceLayoutSource? layouts)
    {
        _templateResolution = templateResolution;
        _activationReader = activationReader;
        _availabilityEvaluator = availabilityEvaluator;
        _surfaceStore = surfaceStore;
        _layouts = layouts;
    }

    public async Task<IReadOnlyList<string>> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        await ResolveAsync(workspaceKey, surfaceKey: null, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<string>> ResolveAsync(
        string workspaceKey,
        string? surfaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        var normalizedKey = workspaceKey.Trim();

        var effectiveTemplates = await _templateResolution
            .ResolveAsync(normalizedKey, cancellationToken)
            .ConfigureAwait(false);
        var activePluginIds = await _activationReader
            .ListActivePluginIdsAsync(normalizedKey, cancellationToken)
            .ConfigureAwait(false);

        var chain = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ownedByAnApp = false;

        if (_surfaceStore is not null && !string.IsNullOrWhiteSpace(surfaceKey))
        {
            var surface = await _surfaceStore
                .GetAsync(normalizedKey, surfaceKey.Trim(), cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(surface?.TemplatePluginId) &&
                seen.Add(surface.TemplatePluginId))
            {
                chain.Add(surface.TemplatePluginId);
                ownedByAnApp = true;
            }
        }

        foreach (var template in effectiveTemplates)
        {
            if (seen.Add(template.PluginId))
            {
                chain.Add(template.PluginId);
            }
        }

        // Eine INHALTSFLÄCHE zeigt, was ihr Layout verlangt — und sonst nichts.
        //
        // Hier und nicht im Renderpfad: Der Client holt die Kette über einen eigenen Endpunkt und
        // lädt danach seine Bundles. Läge die Kürzung nur im Renderpfad, käme das Server-Markup
        // sauber und der Browser mountete trotzdem jeden Block, den irgendein aktives Plugin
        // mitbringt — genau der Zustand, der wie ein Rendering-Fehler aussieht und keiner ist.
        if (!ownedByAnApp && _layouts is not null && !string.IsNullOrWhiteSpace(surfaceKey))
        {
            var document = await _layouts
                .GetPublishedAsync(normalizedKey, surfaceKey.Trim(), cancellationToken)
                .ConfigureAwait(false);

            var needed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var blockId in document?.Sections.SelectMany(section => section.Blocks) ?? [])
            {
                var separator = blockId.BlockId.IndexOf('.', StringComparison.Ordinal);
                needed.Add(separator > 0 ? blockId.BlockId[..separator] : blockId.BlockId);
            }

            // Das Theme steht schon in der Kette und bleibt: Es gestaltet, es rendert nicht.
            foreach (var template in effectiveTemplates)
            {
                needed.Add(template.PluginId);
            }

            return chain.Where(needed.Contains).ToList();
        }

        // Gehört die Fläche einer App, ist die Kette DAMIT zu Ende.
        //
        // Sonst steuerte jedes im Workspace aktive Plugin seine Oberfläche zu jeder Fläche bei:
        // Ein Konferenzraum zeigte die Telefon-Blöcke des Communication-Plugins, eine leere
        // Inhaltsfläche zeigte alles, was installiert ist. Die Zuweisung ist die Entscheidung des
        // Betreibers, welche Anwendung hier läuft — und eine Anwendung, in die sich jede andere
        // hineinrendert, ist keine.
        //
        // Das Theme steht schon in der Kette (es kam vor dieser Schleife) und bleibt: Es
        // gestaltet, es rendert nicht.
        if (ownedByAnApp)
        {
            return chain;
        }

        foreach (var pluginId in activePluginIds)
        {
            if (!seen.Add(pluginId))
            {
                continue;
            }

            // An active plugin only contributes UI when it is effectively
            // available in the workspace (REV2 §13): a lapsed entitlement,
            // missing capability or an unhealthy runtime drops it from the chain
            // without touching its desired activation.
            var availability = await _availabilityEvaluator
                .EvaluateAsync(pluginId, normalizedKey, cancellationToken)
                .ConfigureAwait(false);
            if (availability.IsAvailable)
            {
                chain.Add(pluginId);
            }
        }

        return chain;
    }
}
