namespace Callora.Host.Backend.Application.Lifecycle;

internal sealed class WorkspaceLifecycleLock
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);

    public int ReferenceCount;
}
