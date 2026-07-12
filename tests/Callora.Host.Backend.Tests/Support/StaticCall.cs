using Callora.Contracts.Communication;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Contract-only fake call without any protocol backing. Serves as second
/// ICall implementation to keep the communication contracts channel-neutral.
/// </summary>
public sealed class StaticCall : ICall
{
    private readonly List<char> _sentDtmfTones = [];
    private CallState _state;

    public StaticCall(
        CallTarget target,
        CallDirection direction = CallDirection.Outbound,
        CallState initialState = CallState.Connecting)
    {
        CallId = Guid.NewGuid().ToString("N");
        Target = target;
        Direction = direction;
        _state = initialState;
    }

    public string CallId { get; }

    public CallState State => _state;

    public CallDirection Direction { get; }

    public CallTarget Target { get; }

    public IReadOnlyList<char> SentDtmfTones => _sentDtmfTones;

    public event EventHandler<CallStateChangedEventArgs>? StateChanged;

    public Task AcceptAsync(CancellationToken cancellationToken = default)
    {
        EnsureInboundRinging("accept");
        TransitionTo(CallState.Connected);
        return Task.CompletedTask;
    }

    public Task RejectAsync(CancellationToken cancellationToken = default)
    {
        EnsureInboundRinging("reject");
        TransitionTo(CallState.Terminated);
        return Task.CompletedTask;
    }

    public Task HangupAsync(CancellationToken cancellationToken = default)
    {
        TransitionTo(CallState.Terminated);
        return Task.CompletedTask;
    }

    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default)
    {
        _sentDtmfTones.Add(tone);
        return Task.CompletedTask;
    }

    public void TransitionTo(CallState newState)
    {
        var previous = _state;
        if (previous == newState || previous == CallState.Terminated)
            return;

        _state = newState;
        StateChanged?.Invoke(this, new CallStateChangedEventArgs(previous, newState));
    }

    private void EnsureInboundRinging(string action)
    {
        if (Direction != CallDirection.Inbound || _state != CallState.Ringing)
        {
            throw new InvalidOperationException(
                $"Cannot {action} a call that is not inbound and ringing (direction: {Direction}, state: {_state}).");
        }
    }
}
