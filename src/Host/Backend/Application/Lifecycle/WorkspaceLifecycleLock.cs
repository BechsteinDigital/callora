namespace Callora.Host.Backend.Application.Lifecycle;

/// <summary>
/// Reference-counted async lock state for one workspace/plugin pair.
/// </summary>
public sealed class WorkspaceLifecycleLock
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);

    public int ReferenceCount;
}
