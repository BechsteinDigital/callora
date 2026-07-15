using Callora.Host.Backend.Application.Workspaces;
using Callora.Host.Backend.Tests.Support;
using Callora.Host.PluginContracts.Application.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Host.Backend.Tests.Application.Workspaces;

public sealed class PluginWorkspaceDataPurgerTests
{
    [Fact]
    public async Task PurgeAsync_InvokesEveryExportedContributor()
    {
        var first = new RecordingContributor();
        var second = new RecordingContributor();
        var purger = new PluginWorkspaceDataPurger(
            Catalog(first, second),
            NullLogger<PluginWorkspaceDataPurger>.Instance);

        await purger.PurgeAsync("workspace-a");

        Assert.Equal("workspace-a", first.PurgedWorkspaceKey);
        Assert.Equal("workspace-a", second.PurgedWorkspaceKey);
    }

    [Fact]
    public async Task PurgeAsync_OneContributorThrows_OthersStillRun()
    {
        var throwing = new ThrowingContributor();
        var recording = new RecordingContributor();
        var purger = new PluginWorkspaceDataPurger(
            Catalog(throwing, recording),
            NullLogger<PluginWorkspaceDataPurger>.Instance);

        // Best-effort: the failing contributor is swallowed (logged), not fatal.
        await purger.PurgeAsync("workspace-a");

        Assert.Equal("workspace-a", recording.PurgedWorkspaceKey);
    }

    private static StaticPluginCatalog Catalog(params IWorkspaceDataPurgeContributor[] contributors) =>
        new(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IWorkspaceDataPurgeContributor)] = contributors.Cast<object>().ToArray()
        });

    private sealed class RecordingContributor : IWorkspaceDataPurgeContributor
    {
        public string? PurgedWorkspaceKey { get; private set; }

        public Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
        {
            PurgedWorkspaceKey = workspaceKey;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingContributor : IWorkspaceDataPurgeContributor
    {
        public Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }
}
