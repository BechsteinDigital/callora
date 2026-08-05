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

    // Base64url like the production codec's output, so a value produced here can
    // travel in a real Cookie header (raw JSON cannot: quotes, commas and semicolons
    // are not legal cookie-value characters).
    public string Protect(SurfaceSessionEnvelope envelope) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(envelope, Options)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public SurfaceSessionEnvelope? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return JsonSerializer.Deserialize<SurfaceSessionEnvelope>(json, Options);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return null;
        }
    }
}
