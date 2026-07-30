using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// The video/audio outbound track pair over which one participant's peer renders a single source
/// participant's media. Held in <see cref="ConferenceParticipantEntry.Outbound"/>, keyed by the source's
/// id. Both tracks are created send-only with <c>StreamId = sourceParticipantId</c> so the browser
/// attributes them to the correct sender.
/// </summary>
/// <param name="Video">The send-only video track carrying the source's camera to this peer.</param>
/// <param name="Audio">The send-only audio track carrying the source's microphone to this peer.</param>
internal readonly record struct ConferenceOutboundTracks(IMediaOutboundTrack Video, IMediaOutboundTrack Audio);
