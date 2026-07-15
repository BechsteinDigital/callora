using Callora.Core.Application.Extensions;

namespace Callora.Core.Tests.Support;

internal sealed class EmptyWorkspaceTemplateResolutionService : IWorkspaceTemplateResolutionService
{
    public Task<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>>([]);
    }
}
