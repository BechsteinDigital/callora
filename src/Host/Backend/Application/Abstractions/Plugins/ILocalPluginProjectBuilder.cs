namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public interface ILocalPluginProjectBuilder
{
    Task<LocalPluginProjectBuildResult> BuildAsync(
        string projectPath,
        bool forceRebuild = false,
        CancellationToken cancellationToken = default);
}
