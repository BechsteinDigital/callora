namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Payload of <see cref="ICommunicationChannel.HealthChanged"/>: the channel's health transitioned to
/// <see cref="Health"/>. Lets consumers react to availability changes without polling
/// <see cref="ICommunicationChannel.Health"/>.
/// </summary>
public sealed class ChannelHealthChangedEventArgs : EventArgs
{
    /// <summary>Creates the payload for one health transition.</summary>
    public ChannelHealthChangedEventArgs(ChannelHealth health) => Health = health;

    /// <summary>The channel's health after the transition.</summary>
    public ChannelHealth Health { get; }
}
