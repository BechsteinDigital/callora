using System.Collections.Concurrent;
using Callora.Plugin.Communication.Abstractions;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Bridges a live <see cref="IVoipCall"/>'s lifecycle to the <see cref="SdkCallAudioStreamProvider"/>
/// that backs the WebSocket media surface: when a tracked call reaches <see cref="CallState.Connected"/>
/// it opens the call's audio bridge and registers it by call id, and when the call terminates it removes
/// and disposes that stream. A WebSocket consumer then resolves live call audio by call id.
/// </summary>
/// <remarks>
/// Registration/teardown are driven from the call's <see cref="ICall.StateChanged"/> event, which the
/// SDK raises synchronously on its signaling thread — so the work is dispatched fire-and-forget and
/// must not block. Opening the audio bridge and disposing it complete synchronously (they only wire a
/// media tap), so tracking is effectively immediate. Failures are logged, never thrown back onto the
/// signaling thread.
/// </remarks>
public sealed class SdkCallAudioRegistrar
{
    private readonly SdkCallAudioStreamProvider _provider;
    private readonly ILogger<SdkCallAudioRegistrar> _logger;
    private readonly ConcurrentDictionary<string, TrackedCall> _tracked = new(StringComparer.Ordinal);

    /// <summary>Creates a registrar that populates <paramref name="provider"/> from tracked calls.</summary>
    public SdkCallAudioRegistrar(SdkCallAudioStreamProvider provider, ILogger<SdkCallAudioRegistrar> logger)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(logger);

        _provider = provider;
        _logger = logger;
    }

    /// <summary>
    /// Starts tracking one call's audio lifecycle. Idempotent per call id; a call that is already
    /// terminated is ignored, and one that is already connected registers immediately.
    /// </summary>
    public void Track(IVoipCall call)
    {
        ArgumentNullException.ThrowIfNull(call);

        if (call.State == CallState.Terminated)
        {
            return;
        }

        void Handler(object? sender, CallStateChangedEventArgs e) => OnStateChanged(call, e.CurrentState);

        if (!_tracked.TryAdd(call.CallId, new TrackedCall(call, Handler)))
        {
            return; // already tracked
        }

        call.StateChanged += Handler;

        // A call connected before tracking started replays no state event — register it now.
        if (call.State == CallState.Connected)
        {
            _ = RegisterAsync(call);
        }
    }

    /// <summary>Releases every tracked call's stream and unsubscribes — for plugin shutdown.</summary>
    public async Task ClearAsync()
    {
        foreach (var callId in _tracked.Keys.ToArray())
        {
            if (_tracked.TryRemove(callId, out var tracked))
            {
                tracked.Call.StateChanged -= tracked.Handler;
                await DisposeStreamAsync(callId).ConfigureAwait(false);
            }
        }
    }

    private void OnStateChanged(IVoipCall call, CallState state)
    {
        switch (state)
        {
            case CallState.Connected:
                _ = RegisterAsync(call);
                break;
            case CallState.Terminated:
                _ = ReleaseAsync(call);
                break;
        }
    }

    private async Task RegisterAsync(IVoipCall call)
    {
        try
        {
            var stream = await call.OpenAudioAsync().ConfigureAwait(false);
            _provider.Register(call.CallId, stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open/register audio for call {CallId}.", call.CallId);
        }
    }

    private async Task ReleaseAsync(IVoipCall call)
    {
        if (_tracked.TryRemove(call.CallId, out var tracked))
        {
            tracked.Call.StateChanged -= tracked.Handler;
        }

        await DisposeStreamAsync(call.CallId).ConfigureAwait(false);
    }

    private async Task DisposeStreamAsync(string callId)
    {
        var stream = _provider.Remove(callId);
        if (stream is null)
        {
            return;
        }

        try
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispose audio for call {CallId}.", callId);
        }
    }
}
