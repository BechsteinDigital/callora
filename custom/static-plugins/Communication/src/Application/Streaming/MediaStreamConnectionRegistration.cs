namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Handle for one socket registered in the <see cref="MediaStreamConnectionRegistry"/>. Disposing it
/// removes the registration; disposing twice is a no-op.
/// </summary>
public sealed class MediaStreamConnectionRegistration : IDisposable
{
    private readonly MediaStreamConnectionRegistry _registry;
    private readonly string _callId;
    private readonly string _sessionId;
    private bool _disposed;

    internal MediaStreamConnectionRegistration(MediaStreamConnectionRegistry registry, string callId, string sessionId)
    {
        _registry = registry;
        _callId = callId;
        _sessionId = sessionId;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registry.Unregister(_callId, _sessionId);
    }
}
