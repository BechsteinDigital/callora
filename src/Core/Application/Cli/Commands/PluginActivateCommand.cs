using Callora.Core.Application.Lifecycle;

namespace Callora.Core.Application.Cli.Commands;

/// <summary><c>plugin:activate &lt;pluginId&gt;</c> — activates an installed plugin.</summary>
internal sealed class PluginActivateCommand(IPluginLifecycleService lifecycleService)
    : PluginConsoleCommandBase(lifecycleService)
{
    public override string Name => "plugin:activate";

    public override string Description => "Activate an installed plugin.";

    protected override Task<PluginLifecycleServiceResult> RunAsync(string pluginId, CancellationToken cancellationToken)
        => LifecycleService.ActivateAsync(new PluginLifecycleCommand(pluginId, "cli:plugin-activate", WorkspaceKey: null), cancellationToken);
}
