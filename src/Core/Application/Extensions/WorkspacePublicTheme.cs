namespace Callora.Core.Application.Extensions;

/// <summary>
/// Publicly consumable theme values of one workspace: the assigned theme
/// plugin and its effective setting values (defaults merged with workspace
/// overrides), normalized to plain strings for CSS consumption.
/// </summary>
/// <param name="SectionLayouts">
/// The section layouts this theme offers, with their regions. Part of the public theme because
/// they are structure the theme owns, and both consumers need them: the editor to offer them,
/// the composition renderer to notice a layout the theme no longer knows (§7.8).
/// </param>
public sealed record WorkspacePublicTheme(
    string ThemePluginId,
    string ThemeVersion,
    IReadOnlyDictionary<string, string> ValuesByKey,
    IReadOnlyList<SectionLayoutDefinition> SectionLayouts);
