using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Activation reader fake returning a fixed active plugin list.
/// </summary>
public sealed class StaticWorkspacePluginActivationReader(
    IReadOnlyList<string> activePluginIds) : IWorkspacePluginActivationReader
{
    public Task<IReadOnlyList<string>> ListActivePluginIdsAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(activePluginIds);
}
