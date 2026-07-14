namespace Callora.Host.Backend.Application.Plugins;

public interface ILocalPluginInstallSourceResolver
{
    Task<LocalPluginInstallSourceResolveResult> ResolveForInstallAsync(
        string pluginId,
        bool buildIfNeeded,
        bool forceBuild = false,
        CancellationToken cancellationToken = default);
}
