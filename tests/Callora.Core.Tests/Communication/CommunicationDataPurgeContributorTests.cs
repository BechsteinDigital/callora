using System;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Application.Compliance;
using Xunit;

namespace Callora.Core.Tests.Communication;

/// <summary>
/// The GDPR purge contributor (F3) is a thin Application seam: it validates the workspace and
/// delegates the actual erasure to the atomic <see cref="ICommunicationWorkspaceDataPurger"/>.
/// </summary>
public sealed class CommunicationDataPurgeContributorTests
{
    [Fact]
    public async Task PurgeWorkspaceAsync_DelegatesToAtomicPurger()
    {
        var purger = new RecordingPurger();
        var contributor = new CommunicationDataPurgeContributor(purger);

        await contributor.PurgeWorkspaceAsync("ws-x");

        Assert.Equal("ws-x", purger.PurgedWorkspace);
    }

    [Fact]
    public async Task PurgeWorkspaceAsync_RejectsBlankWorkspace()
    {
        var contributor = new CommunicationDataPurgeContributor(new RecordingPurger());

        await Assert.ThrowsAsync<ArgumentException>(() => contributor.PurgeWorkspaceAsync("  "));
    }
}

internal sealed class RecordingPurger : ICommunicationWorkspaceDataPurger
{
    public string? PurgedWorkspace { get; private set; }

    public Task PurgeAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        PurgedWorkspace = workspaceKey;
        return Task.CompletedTask;
    }
}
