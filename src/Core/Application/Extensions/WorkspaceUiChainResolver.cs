using Callora.Core.Application.Extensions;
using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Extensions;

/// <summary>
/// Resolves the ordered plugin UI load chain for one workspace: template
/// plugins first, in effective template resolution order, followed by the
/// workspace's remaining active plugins. The shells load UI bundles in this
/// order so later bundles can extend blocks contributed by earlier ones.
/// </summary>
public sealed class WorkspaceUiChainResolver(
    IWorkspaceTemplateResolutionService templateResolution,
    IWorkspacePluginActivationReader activationReader,
    IPluginAvailabilityEvaluator availabilityEvaluator)
{
    public async Task<IReadOnlyList<string>> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        var normalizedKey = workspaceKey.Trim();

        var effectiveTemplates = await templateResolution
            .ResolveAsync(normalizedKey, cancellationToken)
            .ConfigureAwait(false);
        var activePluginIds = await activationReader
            .ListActivePluginIdsAsync(normalizedKey, cancellationToken)
            .ConfigureAwait(false);

        var chain = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in effectiveTemplates)
        {
            if (seen.Add(template.PluginId))
            {
                chain.Add(template.PluginId);
            }
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
            var availability = await availabilityEvaluator
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
