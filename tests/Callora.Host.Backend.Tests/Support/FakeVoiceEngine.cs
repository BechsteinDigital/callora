using Callora.Contracts.Communication;
using Callora.Plugins.Voip.Application.Accounts;
using Callora.Plugins.Voip.Application.Channels;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Voice engine fake recording placed calls; returns controllable engine calls.
/// </summary>
public sealed class FakeVoiceEngine : IVoiceEngine
{
    private readonly List<(SipAccountEntry Account, CallTarget Target, FakeEngineCall Call)> _placedCalls = [];

    public IReadOnlyList<(SipAccountEntry Account, CallTarget Target, FakeEngineCall Call)> PlacedCalls => _placedCalls;

    public bool IsDisposed { get; private set; }

    public Task<IEngineCall> PlaceCallAsync(
        SipAccountEntry account,
        CallTarget target,
        CancellationToken cancellationToken = default)
    {
        var call = new FakeEngineCall();
        _placedCalls.Add((account, target, call));
        return Task.FromResult<IEngineCall>(call);
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
