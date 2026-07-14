using System.Text.RegularExpressions;

namespace Callora.Host.Backend.Infrastructure.Persistence;

/// <summary>
/// Derives and validates the dedicated Postgres schema name a plugin uses
/// for its own EF database (PLAT-260). The convention is
/// <c>plugin_&lt;id&gt;</c>; the id is strictly sanitized because schema
/// names cannot be parameterized in DDL.
/// </summary>
public static class PluginSchemaName
{
    private static readonly Regex SafeIdentifier =
        new("^[a-z][a-z0-9_]{0,60}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns the convention schema name <c>plugin_&lt;id&gt;</c>, or null
    /// when the id is not safe for an unquoted-style identifier.
    /// </summary>
    public static string? TryResolve(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return null;
        }

        var normalized = pluginId.Trim().ToLowerInvariant().Replace('-', '_');
        return normalized.Length <= 48 && SafeIdentifier.IsMatch(normalized) ? $"plugin_{normalized}" : null;
    }

    /// <summary>
    /// Validates an already-complete schema name (e.g. one declared in a
    /// plugin manifest). Returns the normalized name or null when unsafe.
    /// </summary>
    public static string? Sanitize(string? schemaName)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            return null;
        }

        var normalized = schemaName.Trim().ToLowerInvariant();
        return SafeIdentifier.IsMatch(normalized) ? normalized : null;
    }
}
