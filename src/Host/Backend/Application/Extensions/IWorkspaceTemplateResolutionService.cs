namespace Callora.Host.Backend.Application.Extensions;

public interface IWorkspaceTemplateResolutionService
{
    Task<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);
}
