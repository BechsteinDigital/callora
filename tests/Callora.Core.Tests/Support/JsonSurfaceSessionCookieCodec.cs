using System.Text.Json;
using Callora.Core.Application.Surfaces;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Cookie codec without cryptography: round-trips the envelope as plain JSON so a
/// test can read what the host put in the cookie. The production codec protects the
/// same payload with the host key ring.
/// </summary>
public sealed class JsonSurfaceSessionCookieCodec : ISurfaceSessionCookieCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string Protect(SurfaceSessionEnvelope envelope) =>
        JsonSerializer.Serialize(envelope, Options);

    public SurfaceSessionEnvelope? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SurfaceSessionEnvelope>(value, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
