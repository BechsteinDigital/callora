using Callora.Core.Application.Lifecycle;

namespace Callora.Core.Application.Cli.Commands;

/// <summary><c>plugin:update &lt;pluginId&gt;</c> — updates a plugin from its local source.</summary>
internal sealed class PluginUpdateCommand(IPluginLifecycleService lifecycleService)
    : PluginConsoleCommandBase(lifecycleService)
{
    public override string Name => "plugin:update";

    public override string Description => "Update a plugin from its local source (builds if needed).";

    protected override Task<PluginLifecycleServiceResult> RunAsync(string pluginId, CancellationToken cancellationToken)
        => LifecycleService.UpdateFromLocalAsync(
            new UpdateLocalPluginCommand(pluginId, BuildIfNeeded: true, ForceBuild: false, RequestedBy: "cli:plugin-update"),
            cancellationToken);
}
