namespace Callora.Plugin.Communication.Abstractions.RealtimeMedia;

/// <summary>
/// A transport-neutral WebRTC session description (RFC 8829): an offer or an answer carried as its SDP
/// string. It crosses the plugin boundary in both directions — a consumer relays it to/from the browser
/// over its own authenticated transport, and communication produces/applies it through the media provider
/// port. Neutral by design: it carries no SDK type, so both the consumer contracts (calls, conferences)
/// and any provider adapter share one signalling value.
/// </summary>
/// <param name="Type">The description kind: <c>"offer"</c> or <c>"answer"</c> (RFC 8829 SDP type).</param>
/// <param name="Sdp">The raw SDP payload.</param>
public sealed record SessionDescription(string Type, string Sdp);
