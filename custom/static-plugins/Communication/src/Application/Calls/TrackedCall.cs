using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Calls;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// One live call tracked by <see cref="CallControlService"/>: the underlying <see cref="ICall"/>, its
/// persisted <see cref="CallLog"/> and the state-changed handler kept so it can be detached on teardown.
/// </summary>
internal sealed class TrackedCall
{
    public TrackedCall(
        string workspaceKey,
        ICall call,
        CallLog log,
        EventHandler<CallStateChangedEventArgs> handler)
    {
        WorkspaceKey = workspaceKey;
        Call = call;
        Log = log;
        Handler = handler;
    }

    public string WorkspaceKey { get; }

    public ICall Call { get; }

    public CallLog Log { get; }

    public EventHandler<CallStateChangedEventArgs> Handler { get; }
}
