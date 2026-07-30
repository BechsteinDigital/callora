namespace Callora.Plugin.Communication.Application.RealtimeMedia;

/// <summary>
/// The single downward port to a real-time media SDK: the media layer (calls, SFU) creates neutral
/// <see cref="IMediaPeer"/> instances through it and never touches a provider SDK. The
/// <c>CalloraVoipSdkProvider</c> is the first adapter; another WebRTC-shaped SDK can be adapted behind the
/// same port without changing any consumer (ADR-016).
/// </summary>
internal interface IRealtimeMediaProvider
{
    /// <summary>Creates a new, unconnected server-side media peer configured by <paramref name="options"/>.
    /// The caller owns the peer and disposes it.</summary>
    IMediaPeer CreatePeer(MediaPeerOptions options);
}
