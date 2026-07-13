namespace Callora.Host.Backend.Application.Lifecycle;

/// <summary>
/// Reference-counted async lock state for one workspace/plugin pair.
/// </summary>
public sealed class WorkspaceLifecycleLock
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);

    // Feld statt Property, weil Interlocked.Increment/Decrement eine
    // ref-Übergabe braucht; internal hält es aus der öffentlichen API.
    internal int ReferenceCount;
}
