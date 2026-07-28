using System.Text.Json;
using System.Text.Json.Serialization;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// One signalling frame on the WebRTC signalling WebSocket, in the small JSON wire form
/// <c>{ "type": "offer|answer|candidate", "sdp"?: string, "candidate"?: string }</c>. Callora is the
/// offerer (see the SDK browser-interop flow): it sends <c>offer</c> and <c>candidate</c> frames and
/// receives <c>answer</c> and <c>candidate</c> frames. Only the field carried by a given type is set;
/// the others stay <see langword="null"/>.
/// </summary>
/// <param name="Type">The frame type: <c>offer</c>, <c>answer</c> or <c>candidate</c>.</param>
/// <param name="Sdp">Session description (present on <c>offer</c>/<c>answer</c>).</param>
/// <param name="Candidate">The RFC 8829 <c>candidate:</c> line (present on <c>candidate</c>).</param>
public sealed record WebRtcSignalMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("sdp")] string? Sdp = null,
    [property: JsonPropertyName("candidate")] string? Candidate = null)
{
    /// <summary>The <c>offer</c> type Callora sends after producing its local description.</summary>
    public const string TypeOffer = "offer";

    /// <summary>The <c>answer</c> type the browser sends in reply to the offer.</summary>
    public const string TypeAnswer = "answer";

    /// <summary>The <c>candidate</c> type carrying a trickled ICE candidate in either direction.</summary>
    public const string TypeCandidate = "candidate";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Builds the outbound <c>offer</c> frame carrying the local SDP.</summary>
    public static WebRtcSignalMessage Offer(string sdp) => new(TypeOffer, Sdp: sdp);

    /// <summary>Builds the outbound <c>candidate</c> frame carrying one local ICE candidate line.</summary>
    public static WebRtcSignalMessage IceCandidate(string candidate) => new(TypeCandidate, Candidate: candidate);

    /// <summary>Serializes this frame to its JSON wire form (camelCase, nulls omitted).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Parses a JSON frame, or returns <see langword="null"/> when it is malformed or has no string
    /// <c>type</c> — so a misbehaving client cannot crash the handler.
    /// </summary>
    public static WebRtcSignalMessage? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var message = JsonSerializer.Deserialize<WebRtcSignalMessage>(json, SerializerOptions);
            return string.IsNullOrWhiteSpace(message?.Type) ? null : message;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
