namespace Callora.Core.Domain.Plugins;

/// <summary>
/// Encodes capability code lists into one storage string and back.
/// </summary>
public static class CapabilityListCodec
{
    private const char Separator = ';';

    /// <summary>
    /// Joins one capability list into a storage string, or null when empty.
    /// </summary>
    public static string? Join(IReadOnlyList<string>? capabilities)
    {
        if (capabilities is null || capabilities.Count == 0)
            return null;

        var normalized = capabilities
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join(Separator, normalized);
    }

    /// <summary>
    /// Splits one storage string into a capability list.
    /// </summary>
    public static IReadOnlyList<string> Split(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            return Array.Empty<string>();

        return encoded
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
