namespace Callora.Plugin.Communication.Domain.Streaming;

/// <summary>Lifecycle of a <see cref="MediaStreamSession"/>.</summary>
public enum MediaStreamSessionStatus
{
    /// <summary>Created; the connect token has not been redeemed yet.</summary>
    Pending,

    /// <summary>The consumer connected and audio is flowing.</summary>
    Active,

    /// <summary>The stream ended.</summary>
    Closed
}
