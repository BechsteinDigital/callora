using Callora.Host.Backend.Application.Abstractions.Extensions;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Template resolution fake returning a fixed effective chain.
/// </summary>
public sealed class StaticWorkspaceTemplateResolutionService(
    IReadOnlyList<WorkspaceTemplateEffectiveSnapshot> snapshots) : IWorkspaceTemplateResolutionService
{
    public Task<IReadOnlyList<WorkspaceTemplateEffectiveSnapshot>> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(snapshots);
}
