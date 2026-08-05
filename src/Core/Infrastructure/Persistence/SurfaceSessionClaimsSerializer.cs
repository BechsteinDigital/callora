using System.Text.Json;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Stores a session's claim bag as JSON. The claims are already validated and bounded
/// by the host before they get here (ADR-017 §3.1), so this only has to round-trip
/// them — and to survive a row written by an older shape without throwing.
/// </summary>
internal static class SurfaceSessionClaimsSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyDictionary<string, IReadOnlyList<string>> claims) =>
        JsonSerializer.Serialize(
            claims.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal),
            Options);

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json, Options);
            return raw is null
                ? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                : raw.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // An unreadable claim bag must not take the session down: the identity
            // itself is still valid, it simply carries nothing the plugin can use.
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }
    }
}
