using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Channels;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Voice engine fake recording placed calls; returns controllable engine calls.
/// </summary>
public sealed class FakeVoiceEngine : IVoiceEngine
{
    private readonly List<(SipAccountEntry Account, CallTarget Target, FakeEngineCall Call)> _placedCalls = [];
    private readonly List<FakeIncomingCallSubscription> _incomingCallSubscriptions = [];

    public IReadOnlyList<(SipAccountEntry Account, CallTarget Target, FakeEngineCall Call)> PlacedCalls => _placedCalls;

    public IReadOnlyList<FakeIncomingCallSubscription> IncomingCallSubscriptions => _incomingCallSubscriptions;

    public bool IsDisposed { get; private set; }

    /// <summary>When set, the next subscription attempt throws this exception.</summary>
    public Exception? NextSubscriptionError { get; set; }

    public Task<IEngineCall> PlaceCallAsync(
        SipAccountEntry account,
        CallTarget target,
        CancellationToken cancellationToken = default)
    {
        var call = new FakeEngineCall();
        _placedCalls.Add((account, target, call));
        return Task.FromResult<IEngineCall>(call);
    }

    public Task<IDisposable> SubscribeIncomingCallsAsync(
        SipAccountEntry account,
        Action<IEngineCall> onIncomingCall,
        CancellationToken cancellationToken = default)
    {
        if (NextSubscriptionError is not null)
        {
            var error = NextSubscriptionError;
            NextSubscriptionError = null;
            throw error;
        }

        var subscription = new FakeIncomingCallSubscription(account, onIncomingCall);
        _incomingCallSubscriptions.Add(subscription);
        return Task.FromResult<IDisposable>(subscription);
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
