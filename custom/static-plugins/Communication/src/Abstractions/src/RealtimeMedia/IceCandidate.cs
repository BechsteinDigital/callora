namespace Callora.Plugin.Communication.Abstractions.RealtimeMedia;

/// <summary>
/// A transport-neutral ICE candidate (RFC 8839): one connectivity candidate carried as its SDP
/// <c>a=candidate</c> attribute string. Emitted by communication as a local candidate for the consumer to
/// relay to the browser, and applied when a remote candidate arrives — neutral, so it leaks no SDK type
/// across the plugin boundary.
/// </summary>
/// <param name="Candidate">The raw ICE candidate SDP string.</param>
public sealed record IceCandidate(string Candidate);
