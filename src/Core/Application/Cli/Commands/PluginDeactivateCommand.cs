using Callora.Core.Application.Lifecycle;

namespace Callora.Core.Application.Cli.Commands;

/// <summary><c>plugin:deactivate &lt;pluginId&gt;</c> — deactivates an active plugin.</summary>
internal sealed class PluginDeactivateCommand(IPluginLifecycleService lifecycleService)
    : PluginConsoleCommandBase(lifecycleService)
{
    public override string Name => "plugin:deactivate";

    public override string Description => "Deactivate an active plugin (keeps it installed).";

    protected override Task<PluginLifecycleServiceResult> RunAsync(string pluginId, CancellationToken cancellationToken)
        => LifecycleService.DeactivateAsync(new PluginLifecycleCommand(pluginId, "cli:plugin-deactivate", WorkspaceKey: null), cancellationToken);
}
