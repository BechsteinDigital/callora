using Callora.Core.Application.Lifecycle;
using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Cli.Commands;

/// <summary>
/// <c>plugin:list</c> — lists installed plugins and their lifecycle state (Shopware
/// plugin:list). Framework command; the skeleton runner dispatches to it.
/// </summary>
internal sealed class PluginListCommand(IPluginLifecycleService lifecycleService) : ICalloraConsoleCommand
{
    public string Name => "plugin:list";

    public string Description => "List installed plugins and their state.";

    public async Task<int> ExecuteAsync(IReadOnlyList<string> args, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var installations = await lifecycleService.GetInstallationsAsync(cancellationToken).ConfigureAwait(false);
        if (installations.Count == 0)
        {
            output.WriteLine("No plugins installed.");
            return 0;
        }

        foreach (var installation in installations.OrderBy(installation => installation.PluginId, StringComparer.Ordinal))
        {
            output.WriteLine($"  {installation.PluginId,-28}{(PluginInstallationState)installation.State,-12}{installation.DisplayName}");
        }

        return 0;
    }
}
