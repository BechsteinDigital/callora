using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Cli.Commands;

/// <summary>
/// <c>plugin:refresh</c> — reconciles the local plugin directories with the
/// installation registry (Shopware plugin:refresh). Framework command; the skeleton
/// runner dispatches to it.
/// </summary>
internal sealed class PluginRefreshCommand(IPluginDiscoveryService discovery) : ICalloraConsoleCommand
{
    public string Name => "plugin:refresh";

    public string Description => "Reconcile the local plugin directories with the installation registry.";

    public async Task<int> ExecuteAsync(IReadOnlyList<string> args, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var result = await discovery.RefreshAsync(cancellationToken).ConfigureAwait(false);
        output.WriteLine(
            $"Refreshed: {result.Added.Count} added, {result.Updated.Count} updated, " +
            $"{result.RemovedInactive.Count} removed, {result.MissingActive.Count} missing-active.");
        WriteSection(output, "added", result.Added);
        WriteSection(output, "updated", result.Updated);
        WriteSection(output, "removed", result.RemovedInactive);
        WriteSection(output, "missing (active, kept)", result.MissingActive);
        return 0;
    }

    private static void WriteSection(TextWriter output, string label, IReadOnlyList<string> pluginIds)
    {
        foreach (var pluginId in pluginIds)
        {
            output.WriteLine($"  {label}: {pluginId}");
        }
    }
}
