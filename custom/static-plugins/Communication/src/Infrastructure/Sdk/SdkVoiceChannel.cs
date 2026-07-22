using Callora.Plugin.Communication.Abstractions;
using CalloraVoipSdk.Core.Application.Media;
using CalloraVoipSdk.Core.Domain.Lines;
using SdkIncomingCallEventArgs = CalloraVoipSdk.Core.Domain.Events.IncomingCallEventArgs;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Wraps one CalloraVoipSdk <see cref="IPhoneLine"/> as a foundation <see cref="IVoiceChannel"/>:
/// derives channel health from the line's registration state, surfaces inbound SDK calls as
/// foundation <see cref="ICall"/>s (via <see cref="SdkCall"/>), and places outbound calls through
/// the line. Every call it produces is an <see cref="IVoipCall"/> backed by the injected media tap
/// factory, so consumers can open the B4-deep-1 audio bridge on it.
/// </summary>
public sealed class SdkVoiceChannel : IVoiceChannel, IDisposable
{
    private static readonly IReadOnlyCollection<string> VoiceCapability = [CommunicationCapabilities.Voice];

    private readonly IPhoneLine _line;
    private readonly Func<(IMediaReceiver Receiver, IMediaSender Sender)> _mediaTapFactory;
    private int _disposed;

    /// <summary>
    /// Wraps <paramref name="line"/> as a channel. <paramref name="mediaTapFactory"/> creates the
    /// per-call receiver/sender tap handed to each produced <see cref="SdkCall"/>.
    /// </summary>
    public SdkVoiceChannel(
        string channelId,
        string displayName,
        string pluginId,
        IPhoneLine line,
        Func<(IMediaReceiver Receiver, IMediaSender Sender)> mediaTapFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(mediaTapFactory);

        ChannelId = channelId;
        DisplayName = displayName;
        PluginId = pluginId;
        _line = line;
        _mediaTapFactory = mediaTapFactory;
        _line.IncomingCall += OnSdkIncomingCall;
    }

    /// <inheritdoc />
    public string ChannelId { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string PluginId { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Capabilities => VoiceCapability;

    /// <inheritdoc />
    public ChannelHealth Health => MapHealth(_line.State);

    /// <inheritdoc />
    public event EventHandler<IncomingCallEventArgs>? IncomingCall;

    /// <inheritdoc />
    public async Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        var sdkCall = await _line.DialAsync(target.Value, options: null, cancellationToken).ConfigureAwait(false);
        return new SdkCall(sdkCall, _mediaTapFactory);
    }

    private void OnSdkIncomingCall(object? sender, SdkIncomingCallEventArgs e)
    {
        var handler = IncomingCall;
        if (handler is null)
        {
            return;
        }

        // Wrap the SDK call so consumers only ever see the foundation contract (and can open audio).
        handler(this, new IncomingCallEventArgs(new SdkCall(e.Call, _mediaTapFactory)));
    }

    private static ChannelHealth MapHealth(LineState state) => state switch
    {
        LineState.Registered => ChannelHealth.Up,
        LineState.Reconnecting or LineState.RegistrationFailed => ChannelHealth.Degraded,
        LineState.Failed => ChannelHealth.Down,
        // Unregistered / Registering (and any future state) — not yet usable, but not a hard failure.
        _ => ChannelHealth.Unknown,
    };

    /// <summary>Unsubscribes from the line so the channel does not outlive its registration.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _line.IncomingCall -= OnSdkIncomingCall;
    }
}
