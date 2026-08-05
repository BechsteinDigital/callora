using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Voice;

namespace Callora.Core.Tests.Communication.Admin;

/// <summary>
/// Captures what the reconciler projects, so a test can assert the status transitions a
/// channel produced without a database.
/// </summary>
internal sealed class RecordingStatusProjector : ISipAccountStatusProjector
{
    private readonly List<(string WorkspaceKey, string AccountId, SipAccountStatus Status, string? Error)> _projections = [];
    private readonly Lock _sync = new();

    /// <summary>Number of projections recorded so far.</summary>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _projections.Count;
            }
        }
    }

    /// <summary>The most recent projection. Throws when nothing was projected yet.</summary>
    public (string WorkspaceKey, string AccountId, SipAccountStatus Status, string? Error) Last
    {
        get
        {
            lock (_sync)
            {
                return _projections[^1];
            }
        }
    }

    public Task ProjectAsync(
        string workspaceKey,
        string accountId,
        SipAccountStatus status,
        string? error,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _projections.Add((workspaceKey, accountId, status, error));
        }

        return Task.CompletedTask;
    }
}
