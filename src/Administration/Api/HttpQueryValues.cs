using Microsoft.Extensions.Primitives;

namespace Callora.Administration.Api;

/// <summary>
/// Copies an ASP.NET query collection into a framework-neutral, case-insensitive
/// dictionary. Shared by the Admin-API and WebSocket endpoints so the plugin-facing
/// request contracts never expose <c>IQueryCollection</c>.
/// </summary>
internal static class HttpQueryValues
{
    public static IReadOnlyDictionary<string, string[]> Read(IQueryCollection query)
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query)
        {
            result[pair.Key] = ToArray(pair.Value);
        }

        return result;
    }

    private static string[] ToArray(StringValues values)
    {
        if (values.Count == 0)
        {
            return Array.Empty<string>();
        }

        var result = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            result[i] = values[i] ?? string.Empty;
        }

        return result;
    }
}
