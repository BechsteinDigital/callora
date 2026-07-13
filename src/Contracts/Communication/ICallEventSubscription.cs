using System.Threading.Channels;

namespace Callora.Contracts.Communication;

/// <summary>
/// One workspace-scoped subscription on a call event stream. Disposing
/// detaches the subscription; providers complete the reader on shutdown so
/// consumers see a clean end-of-stream.
/// </summary>
public interface ICallEventSubscription : IDisposable
{
    /// <summary>Workspace this subscription is scoped to.</summary>
    string WorkspaceKey { get; }

    /// <summary>Buffered event reader; slow consumers may lose oldest events.</summary>
    ChannelReader<CallStreamEvent> Reader { get; }
}
