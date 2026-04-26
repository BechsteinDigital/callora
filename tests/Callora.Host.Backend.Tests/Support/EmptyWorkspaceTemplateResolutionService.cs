using Callora.Host.Backend.Application.Abstractions.Extensions;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class EmptyWorkspaceTemplateResolutionService : IWorkspaceTemplateResolutionService
{
    public Task<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>>([]);
    }
}
