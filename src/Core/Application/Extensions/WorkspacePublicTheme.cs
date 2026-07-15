namespace Callora.Core.Application.Extensions;

/// <summary>
/// Publicly consumable theme values of one workspace: the assigned theme
/// plugin and its effective setting values (defaults merged with workspace
/// overrides), normalized to plain strings for CSS consumption.
/// </summary>
public sealed record WorkspacePublicTheme(
    string ThemePluginId,
    string ThemeVersion,
    IReadOnlyDictionary<string, string> ValuesByKey);
