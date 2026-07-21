using System.Text;
using System.Text.Json;

namespace Callora.Plugin.Communication.Application.Streaming.Protocol;

/// <summary>
/// Encodes and decodes <see cref="MediaStreamMessage"/> to/from the Twilio-Media-Streams-style
/// JSON wire format (§5.3): <c>{"event":"media","media":{"payload":"&lt;b64&gt;"}}</c>,
/// <c>{"event":"start",…}</c>, <c>{"event":"mark","mark":{"name":…}}</c>, <c>{"event":"stop"}</c>,
/// <c>{"event":"clear"}</c>. Unknown or malformed frames decode to <see langword="null"/> so a
/// misbehaving consumer cannot crash the bridge.
/// </summary>
public static class MediaStreamMessageCodec
{
    /// <summary>Serializes a message to its JSON wire form.</summary>
    public static string Encode(MediaStreamMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("event", EventLabel(message.Event));

            switch (message.Event)
            {
                case MediaStreamEventType.Media:
                    writer.WriteStartObject("media");
                    writer.WriteString("payload", message.Payload ?? string.Empty);
                    writer.WriteEndObject();
                    break;

                case MediaStreamEventType.Mark:
                    writer.WriteStartObject("mark");
                    writer.WriteString("name", message.MarkName ?? string.Empty);
                    writer.WriteEndObject();
                    break;

                case MediaStreamEventType.Start when message.Start is { } start:
                    writer.WriteStartObject("start");
                    writer.WriteString("sessionId", start.SessionId);
                    writer.WriteString("callId", start.CallId);
                    writer.WriteStartObject("mediaFormat");
                    writer.WriteString("encoding", start.Encoding);
                    writer.WriteNumber("sampleRate", start.SampleRateHz);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    break;
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Parses a JSON frame, or returns <see langword="null"/> if it is malformed or unknown.</summary>
    public static MediaStreamMessage? TryDecode(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("event", out var eventElement) ||
                eventElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return eventElement.GetString() switch
            {
                "media" => MediaStreamMessage.Media(ReadNested(root, "media", "payload") ?? string.Empty),
                "mark" => MediaStreamMessage.ForMark(ReadNested(root, "mark", "name") ?? string.Empty),
                "start" => ReadStart(root),
                "clear" => MediaStreamMessage.Clear,
                "stop" => MediaStreamMessage.Stop,
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string EventLabel(MediaStreamEventType type) => type switch
    {
        MediaStreamEventType.Start => "start",
        MediaStreamEventType.Media => "media",
        MediaStreamEventType.Stop => "stop",
        MediaStreamEventType.Mark => "mark",
        MediaStreamEventType.Clear => "clear",
        _ => "unknown"
    };

    private static MediaStreamMessage? ReadStart(JsonElement root)
    {
        if (!root.TryGetProperty("start", out var start) || start.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var sessionId = start.TryGetProperty("sessionId", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString()! : string.Empty;
        var callId = start.TryGetProperty("callId", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()! : string.Empty;
        var encoding = string.Empty;
        var sampleRate = 0;
        if (start.TryGetProperty("mediaFormat", out var format) && format.ValueKind == JsonValueKind.Object)
        {
            encoding = format.TryGetProperty("encoding", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()! : string.Empty;
            sampleRate = format.TryGetProperty("sampleRate", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : 0;
        }

        return MediaStreamMessage.ForStart(new MediaStreamStartMetadata(sessionId, callId, encoding, sampleRate));
    }

    private static string? ReadNested(JsonElement root, string objectName, string propertyName) =>
        root.TryGetProperty(objectName, out var nested) &&
        nested.ValueKind == JsonValueKind.Object &&
        nested.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
