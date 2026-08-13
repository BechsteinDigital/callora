namespace Callora.Core.Application.Plugins;

/// <summary>
/// Turns a set of plugins to activate into a dependency-safe activation order
/// (REV2 §5.1) by reading each plugin's declared capabilities from its registry
/// manifest and running them through <see cref="PluginActivationPlanner"/>. Shared
/// by the startup paths (auto-load and DB rehydration). Without a registry reader,
/// or when a manifest cannot be read, the input order is preserved.
/// </summary>
internal static class PluginActivationOrdering
{
    public static async Task<IReadOnlyList<string>> OrderAsync(
        IReadOnlyList<(string PluginId, string AssemblyPath)> plugins,
        IPluginPackageRegistryReader? registryReader,
        CancellationToken cancellationToken)
    {
        if (registryReader is null || plugins.Count == 0)
        {
            return plugins.Select(static plugin => plugin.PluginId).ToList();
        }

        var nodes = new List<PluginActivationNode>(plugins.Count);
        foreach (var (pluginId, assemblyPath) in plugins)
        {
            var registry = await registryReader.ReadForAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
            var metadata = registry.Registry;
            nodes.Add(new PluginActivationNode(
                pluginId,
                // Stand hier hart auf false, womit der Sortierschlüssel des Planers auf den
                // Eingabeindex zusammenfiel: „Foundation zuerst" war im Produktivpfad ein toter
                // Parameter. Der Weg war doppelt abgeschnitten — die Angabe wurde zwar aus der
                // registry.json geparst, aber nicht bis hierher durchgereicht.
                //
                // Application als Vorgabe und nicht das Quellverzeichnis: Wer die Reihenfolge
                // beeinflussen will, sagt es im Manifest. Eine Fläche, die allein daran hängt,
                // WO ein Paket liegt, wäre beim Verschieben still eine andere.
                IsFoundation: PluginTierResolver.Resolve(metadata?.Tier, PluginTier.Application) == PluginTier.System,
                metadata?.Capabilities ?? [],
                metadata?.RequiredCapabilities ?? []));
        }

        var plan = PluginActivationPlanner.Plan(nodes);

        // Unresolved/cyclic plugins are appended so their (expected) activation denial
        // by the capability guard stays observable instead of silently skipped.
        return plan.Order
            .Concat(plan.UnresolvedDependencies)
            .Concat(plan.Cyclic)
            .ToList();
    }
}
