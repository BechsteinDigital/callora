using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Channels;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Inbound-call subscription handed out by <see cref="FakeVoiceEngine"/>;
/// lets tests raise incoming engine calls and observe disposal.
/// </summary>
public sealed class FakeIncomingCallSubscription(
    SipAccountEntry account,
    Action<IEngineCall> handler) : IDisposable
{
    public SipAccountEntry Account { get; } = account;

    public bool IsDisposed { get; private set; }

    public void RaiseIncomingCall(IEngineCall call) => handler(call);

    public void Dispose() => IsDisposed = true;
}
