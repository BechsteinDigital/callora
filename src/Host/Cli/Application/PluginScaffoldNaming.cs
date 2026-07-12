using System.Text;
using System.Text.RegularExpressions;

namespace Callora.Host.Cli.Application;

internal static class PluginScaffoldNaming
{
    private static readonly Regex InvalidPluginIdRegex = new("[^a-zA-Z0-9._-]", RegexOptions.Compiled);
    private static readonly Regex SplitterRegex = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);

    public static bool IsValidPluginId(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && !InvalidPluginIdRegex.IsMatch(value)
        && !value.StartsWith("-", StringComparison.Ordinal)
        && !value.EndsWith("-", StringComparison.Ordinal)
        && !value.StartsWith(".", StringComparison.Ordinal)
        && !value.EndsWith(".", StringComparison.Ordinal)
        && !value.StartsWith("_", StringComparison.Ordinal)
        && !value.EndsWith("_", StringComparison.Ordinal);

    public static string ToPluginId(string value)
    {
        var segments = SplitterRegex
            .Split(value)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim().ToLowerInvariant())
            .ToArray();

        if (segments.Length == 0)
            return "plugin";

        return string.Join('-', segments);
    }

    public static string ToSafePathSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
                builder.Append(character);
        }

        var normalized = builder.ToString();
        if (string.IsNullOrWhiteSpace(normalized))
            return "Plugin";

        return normalized;
    }

    public static string ToPascalCase(string value)
    {
        var segments = SplitterRegex
            .Split(value)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .ToArray();

        if (segments.Length == 0)
            return "Plugin";

        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            var lowerSegment = segment.ToLowerInvariant();
            builder.Append(char.ToUpperInvariant(lowerSegment[0]));
            if (lowerSegment.Length > 1)
                builder.Append(lowerSegment.AsSpan(1));
        }

        return builder.ToString();
    }
}
