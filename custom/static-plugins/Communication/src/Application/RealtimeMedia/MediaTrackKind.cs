namespace Callora.Plugin.Communication.Application.RealtimeMedia;

/// <summary>
/// The media kind of a real-time track — the neutral counterpart to the provider SDK's track-kind enum.
/// Used both for outbound tracks the media layer adds and for remote tracks it receives.
/// </summary>
internal enum MediaTrackKind
{
    /// <summary>An audio track.</summary>
    Audio,

    /// <summary>A video track.</summary>
    Video,
}
