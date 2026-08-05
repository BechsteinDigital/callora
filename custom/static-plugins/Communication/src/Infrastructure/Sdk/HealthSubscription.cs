namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Detaches a channel health handler on dispose. A tiny handle rather than storing the
/// delegate: the reconciler tears channels down from several paths (disable, reconfigure,
/// delete, shutdown), and each of them only has to dispose what it holds.
/// </summary>
internal sealed class HealthSubscription(Action detach) : IDisposable
{
    private Action? _detach = detach;

    /// <summary>Detaches the handler. Idempotent.</summary>
    public void Dispose()
    {
        var detach = Interlocked.Exchange(ref _detach, null);
        detach?.Invoke();
    }
}
