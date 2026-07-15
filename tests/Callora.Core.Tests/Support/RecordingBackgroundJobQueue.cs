using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Job queue fake recording enqueued requests without executing them.
/// </summary>
public sealed class RecordingBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly List<BackgroundJobRequest> _requests = [];

    public IReadOnlyList<BackgroundJobRequest> Requests => _requests;

    public Task<Guid> EnqueueAsync(BackgroundJobRequest request, CancellationToken cancellationToken = default)
    {
        _requests.Add(request);
        return Task.FromResult(Guid.NewGuid());
    }
}
