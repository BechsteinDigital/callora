namespace Callora.Core.Application.Plugins;

public interface ILocalPluginInstallSourceResolver
{
    Task<LocalPluginInstallSourceResolveResult> ResolveForInstallAsync(
        string pluginId,
        bool buildIfNeeded,
        bool forceBuild = false,
        CancellationToken cancellationToken = default);
}
