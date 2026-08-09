using Callora.Core.Application.Extensions;
using Callora.Core.Application.Plugins;
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
    {
        _templateResolution = templateResolution;
        _activationReader = activationReader;
        _availabilityEvaluator = availabilityEvaluator;
        _surfaceStore = surfaceStore;
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
