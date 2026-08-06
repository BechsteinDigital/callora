using System.Collections.Concurrent;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// The <see cref="ICallDtmfCollector"/> implementation: subscribes to a call's tones for the duration
/// of one entry and hands back what the caller typed.
/// </summary>
internal sealed class CallDtmfCollector : ICallDtmfCollector
{
    private readonly ICallAccess _calls;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, DtmfCollection> _active = new(StringComparer.Ordinal);

    /// <summary>Creates the collector over the call registry and the clock the pause timeout runs on.</summary>
    public CallDtmfCollector(ICallAccess calls, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(calls);

        _calls = calls;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<DtmfEntry> CollectAsync(
        string workspaceKey,
        string callId,
        DtmfCollectOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentNullException.ThrowIfNull(options);

        var call = _calls.Find(workspaceKey, callId)
            ?? throw new InvalidOperationException($"Workspace '{workspaceKey}' has no active call '{callId}'.");

        var collection = new DtmfCollection(call, options, _timeProvider, cancellationToken);

        // One collection per call: two would split the caller's digits between them, and neither
        // would see a complete entry.
        if (_active.TryRemove(callId, out var previous))
        {
            previous.Supersede();
        }

        _active[callId] = collection;
        collection.Start(() => _active.TryRemove(new KeyValuePair<string, DtmfCollection>(callId, collection)));
        return collection.Entry;
    }
}
