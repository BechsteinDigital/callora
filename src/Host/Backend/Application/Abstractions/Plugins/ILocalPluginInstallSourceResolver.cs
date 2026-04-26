namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public interface ILocalPluginInstallSourceResolver
{
    Task<LocalPluginInstallSourceResolveResult> ResolveForInstallAsync(
        string pluginId,
        bool buildIfNeeded,
        bool forceBuild = false,
        CancellationToken cancellationToken = default);
}
