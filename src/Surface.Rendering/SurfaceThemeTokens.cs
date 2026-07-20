namespace Callora.Surface.Rendering;

/// <summary>
/// Composes the allowlisted token dictionary a surface template reads (ADR-015 §8):
/// the workspace's effective, secret-filtered theme setting values (definition
/// defaults merged with per-workspace overrides, as resolved by
/// <c>WorkspacePublicThemeResolver</c>), plus the reserved meta tokens that identify
/// the assigned theme plugin and version.
/// <para>
/// The reserved meta tokens are authoritative — a theme setting key named like a meta
/// token can never shadow it. The host does not invent <c>--cal-*</c> variable names:
/// a template binds a token value onto its own CSS custom property
/// (<c>--cal-color-primary: {{ tokens.primaryColor }}</c>), keeping setting keys and
/// CSS variables decoupled.
/// </para>
/// </summary>
public static class SurfaceThemeTokens
{
    /// <summary>Reserved token key: the id of the workspace's assigned theme plugin.</summary>
    public const string ThemePluginIdKey = "themePluginId";

    /// <summary>Reserved token key: the version of the workspace's assigned theme.</summary>
    public const string ThemeVersionKey = "themeVersion";

    /// <summary>
    /// Builds the token dictionary from the effective theme setting values and the
    /// assigned theme's identity. Effective values are laid down first; the reserved
    /// meta tokens are applied last so they always win over a same-named setting key.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Compose(
        string? themePluginId,
        string? themeVersion,
        IReadOnlyDictionary<string, string>? effectiveValues)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);

        if (effectiveValues is not null)
        {
            foreach (var (key, value) in effectiveValues)
            {
                if (!string.IsNullOrWhiteSpace(key) && value is not null)
                {
                    tokens[key] = value;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(themePluginId))
        {
            tokens[ThemePluginIdKey] = themePluginId;
        }

        if (!string.IsNullOrWhiteSpace(themeVersion))
        {
            tokens[ThemeVersionKey] = themeVersion;
        }

        return tokens;
    }
}
