using System.Net;

namespace Callora.Plugin.Communication.Application.RealtimeMedia;

/// <summary>
/// Neutral, per-peer configuration for <see cref="IRealtimeMediaProvider.CreatePeer"/>. Carries only port
/// value types (no SDK configuration), so the media layer configures a peer without binding a provider SDK.
/// An adapter maps these onto its SDK's peer/client configuration.
/// </summary>
internal sealed record MediaPeerOptions
{
    /// <summary>Audio codecs to offer, by name. Default is Opus — the WebRTC audio standard.</summary>
    public IReadOnlyList<string> AudioCodecs { get; init; } = ["opus"];

    /// <summary>Video codecs to offer, by name (used when <see cref="EnableVideo"/> is set).</summary>
    public IReadOnlyList<string> VideoCodecs { get; init; } = [];

    /// <summary>Whether the peer offers video.</summary>
    public bool EnableVideo { get; init; }

    /// <summary>
    /// Whether to use stable numeric MIDs and append runtime tracks in insertion order for browser-safe
    /// renegotiation. Default false for fixed-track voice peers.
    /// </summary>
    public bool UseStableNumericMediaIds { get; init; }

    /// <summary>ICE helper servers (STUN/TURN) for candidate gathering. Default: none (host-only).</summary>
    public IReadOnlyList<MediaIceServer> IceServers { get; init; } = [];

    /// <summary>Local media endpoint the peer binds. Default is an ephemeral loopback port.</summary>
    public IPEndPoint LocalEndPoint { get; init; } = new(IPAddress.Loopback, 0);
}
