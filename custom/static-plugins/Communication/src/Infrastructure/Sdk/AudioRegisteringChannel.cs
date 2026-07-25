using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Decorates an <see cref="IVoiceChannel"/> so every call it surfaces — inbound (via
/// <see cref="ICommunicationChannel.IncomingCall"/>) and outbound (via
/// <see cref="ICommunicationChannel.PlaceCallAsync"/>) — is handed to the
/// <see cref="SdkCallAudioRegistrar"/>. That wires the call's audio into the WebSocket media surface
/// without coupling the underlying channel to the audio provider. All other members pass through.
/// </summary>
public sealed class AudioRegisteringChannel : IVoiceChannel, IDisposable
{
    private readonly IVoiceChannel _inner;
    private readonly SdkCallAudioRegistrar _registrar;
    private int _disposed;

    /// <summary>Wraps <paramref name="inner"/>, tracking its calls into <paramref name="registrar"/>.</summary>
    public AudioRegisteringChannel(IVoiceChannel inner, SdkCallAudioRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(registrar);

        _inner = inner;
        _registrar = registrar;
        _inner.IncomingCall += OnInnerIncomingCall;
    }

    /// <inheritdoc />
    public string ChannelId => _inner.ChannelId;

    /// <inheritdoc />
    public string DisplayName => _inner.DisplayName;

    /// <inheritdoc />
    public string PluginId => _inner.PluginId;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Capabilities => _inner.Capabilities;

    /// <inheritdoc />
    public ChannelHealth Health => _inner.Health;

    /// <inheritdoc />
    public event EventHandler<ChannelHealthChangedEventArgs>? HealthChanged
    {
        add => _inner.HealthChanged += value;
        remove => _inner.HealthChanged -= value;
    }

    /// <inheritdoc />
    public event EventHandler<IncomingCallEventArgs>? IncomingCall;

    /// <inheritdoc />
    public async Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default)
    {
        var call = await _inner.PlaceCallAsync(target, cancellationToken).ConfigureAwait(false);
        Track(call);
        return call;
    }

    private void OnInnerIncomingCall(object? sender, IncomingCallEventArgs e)
    {
        // Track before re-raising so audio registration is armed before the consumer can drive the call.
        Track(e.Call);
        IncomingCall?.Invoke(this, e);
    }

    private void Track(ICall call)
    {
        if (call is IVoipCall voiceCall)
        {
            _registrar.Track(voiceCall);
        }
    }

    /// <summary>Unsubscribes from the inner channel and disposes it.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _inner.IncomingCall -= OnInnerIncomingCall;
        (_inner as IDisposable)?.Dispose();
    }
}
