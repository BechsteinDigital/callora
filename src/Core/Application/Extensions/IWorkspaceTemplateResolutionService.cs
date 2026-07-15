namespace Callora.Core.Application.Extensions;

public interface IWorkspaceTemplateResolutionService
{
    Task<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);
}
