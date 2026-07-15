using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Channels;

/// <summary>
/// Disposable handle for one channel registration. Disposing removes the
/// channel from the registry exactly once.
/// </summary>
internal sealed class CommunicationChannelRegistration(
    CommunicationChannelRegistry registry,
    string workspaceKey,
    ICommunicationChannel channel) : IDisposable
{
    private int _disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        registry.Unregister(workspaceKey, channel);
    }
}
