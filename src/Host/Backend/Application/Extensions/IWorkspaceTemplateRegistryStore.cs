namespace Callora.Host.Backend.Application.Extensions;

public interface IWorkspaceTemplateRegistryStore
{
    Task<IReadOnlyList<WorkspaceTemplateDefinitionSnapshot>> ListDefinitionsAsync(
        string? surface = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<WorkspaceTemplateDefinitionSnapshot> UpsertDefinitionAsync(
        string templateKey,
        string surface,
        string pluginId,
        string version,
        string displayName,
        string templatePath,
        string? parentTemplateKey,
        string scope,
        bool isActive,
        int priority,
        CancellationToken cancellationToken = default);

    Task<bool> SetDefinitionActivationAsync(
        string templateKey,
        string pluginId,
        string version,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceTemplateDefinitionSnapshot>> ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<WorkspaceTemplateDefinitionInput> definitions,
        CancellationToken cancellationToken = default);
}
