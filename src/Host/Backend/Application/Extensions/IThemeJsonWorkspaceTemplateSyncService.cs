namespace Callora.Host.Backend.Application.Extensions;

public interface IThemeJsonWorkspaceTemplateSyncService
{
    Task SyncFromAssemblyAsync(
        string pluginId,
        string version,
        string assemblyPath,
        CancellationToken cancellationToken = default);

    Task ClearPluginDefinitionsAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
}
