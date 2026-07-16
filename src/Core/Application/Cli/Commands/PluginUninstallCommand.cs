using Callora.Core.Application.Lifecycle;

namespace Callora.Core.Application.Cli.Commands;

/// <summary><c>plugin:uninstall &lt;pluginId&gt;</c> — uninstalls a plugin.</summary>
internal sealed class PluginUninstallCommand(IPluginLifecycleService lifecycleService)
    : PluginConsoleCommandBase(lifecycleService)
{
    public override string Name => "plugin:uninstall";

    public override string Description => "Uninstall a plugin.";

    protected override Task<PluginLifecycleServiceResult> RunAsync(string pluginId, CancellationToken cancellationToken)
        => LifecycleService.UninstallAsync(new PluginLifecycleCommand(pluginId, "cli:plugin-uninstall", WorkspaceKey: null), cancellationToken);
}
