namespace Callora.Core.Application.Plugins;

/// <summary>
/// Computes the order in which installed plugins should be activated so a plugin's
/// required capabilities are provided before it starts (REV2 §5.1): foundation
/// (System-tier) plugins first, then a topological order where a plugin that
/// requires a capability comes after a plugin that provides it. Pure and
/// deterministic — the host/skeleton loader feeds it discovered metadata and
/// activates in the returned order. Plugins whose dependencies are missing or
/// cyclic are reported separately rather than ordered (their activation would be
/// denied by the capability guard anyway).
/// </summary>
internal static class PluginActivationPlanner
{
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>Computes the activation plan for the given installed plugins.</summary>
    public static PluginActivationPlan Plan(IReadOnlyList<PluginActivationNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count == 0)
        {
            return new PluginActivationPlan([], [], []);
        }

        var byId = new Dictionary<string, PluginActivationNode>(Comparer);
        var providers = new Dictionary<string, List<string>>(Comparer);
        foreach (var node in nodes)
        {
            byId[node.PluginId] = node;
            foreach (var capability in node.ProvidedCapabilities)
            {
                if (!providers.TryGetValue(capability, out var owners))
                {
                    providers[capability] = owners = [];
                }

                owners.Add(node.PluginId);
            }
        }

        // A required capability is satisfied when the plugin provides it itself or
        // another still-resolved plugin provides it. Removing an unresolved plugin can
        // strand its dependents, so iterate to a fixed point.
        var resolved = new HashSet<string>(byId.Keys, Comparer);
        var unresolved = new List<string>();
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in nodes)
            {
                if (!resolved.Contains(node.PluginId) || IsSatisfiable(node, resolved, providers))
                {
                    continue;
                }

                resolved.Remove(node.PluginId);
                unresolved.Add(node.PluginId);
                changed = true;
            }
        }

        return TopologicalOrder(nodes, byId, providers, resolved, unresolved);
    }

    private static bool IsSatisfiable(
        PluginActivationNode node,
        HashSet<string> resolved,
        Dictionary<string, List<string>> providers)
    {
        foreach (var capability in node.RequiredCapabilities)
        {
            if (node.ProvidedCapabilities.Contains(capability, Comparer))
            {
                continue;
            }

            var provided = providers.TryGetValue(capability, out var owners)
                && owners.Any(owner => resolved.Contains(owner) && !Comparer.Equals(owner, node.PluginId));
            if (!provided)
            {
                return false;
            }
        }

        return true;
    }

    private static PluginActivationPlan TopologicalOrder(
        IReadOnlyList<PluginActivationNode> nodes,
        Dictionary<string, PluginActivationNode> byId,
        Dictionary<string, List<string>> providers,
        HashSet<string> resolved,
        List<string> unresolved)
    {
        var resolvedNodes = nodes.Where(node => resolved.Contains(node.PluginId)).ToList();
        var inputIndex = new Dictionary<string, int>(Comparer);
        var successors = new Dictionary<string, HashSet<string>>(Comparer);
        var inDegree = new Dictionary<string, int>(Comparer);
        for (var i = 0; i < resolvedNodes.Count; i++)
        {
            inputIndex[resolvedNodes[i].PluginId] = i;
            successors[resolvedNodes[i].PluginId] = new HashSet<string>(Comparer);
            inDegree[resolvedNodes[i].PluginId] = 0;
        }

        // Edge provider -> dependent: the provider must be activated first.
        foreach (var node in resolvedNodes)
        {
            foreach (var capability in node.RequiredCapabilities)
            {
                if (node.ProvidedCapabilities.Contains(capability, Comparer)
                    || !providers.TryGetValue(capability, out var owners))
                {
                    continue;
                }

                foreach (var owner in owners)
                {
                    if (!resolved.Contains(owner) || Comparer.Equals(owner, node.PluginId))
                    {
                        continue;
                    }

                    if (successors[owner].Add(node.PluginId))
                    {
                        inDegree[node.PluginId]++;
                    }
                }
            }
        }

        var order = new List<string>(resolvedNodes.Count);
        var ready = resolvedNodes.Where(node => inDegree[node.PluginId] == 0).ToList();
        while (ready.Count > 0)
        {
            // Foundation first, then original input order — deterministic because
            // inputIndex is unique per plugin.
            ready.Sort((a, b) => a.IsFoundation != b.IsFoundation
                ? (a.IsFoundation ? -1 : 1)
                : inputIndex[a.PluginId].CompareTo(inputIndex[b.PluginId]));

            var next = ready[0];
            ready.RemoveAt(0);
            order.Add(next.PluginId);

            foreach (var successorId in successors[next.PluginId])
            {
                if (--inDegree[successorId] == 0)
                {
                    ready.Add(byId[successorId]);
                }
            }
        }

        var cyclic = resolvedNodes
            .Where(node => inDegree[node.PluginId] > 0)
            .Select(node => node.PluginId)
            .ToList();

        return new PluginActivationPlan(order, unresolved, cyclic);
    }
}
