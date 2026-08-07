namespace Callora.Core.Domain.Extensions;

/// <summary>
/// A section layout a theme plugin declared, as stored.
/// <para>
/// Persisted for the same reason the theme's setting definitions are: the admin has to show what
/// a theme offers, and the renderer has to know whether a stored layout still exists, without
/// re-reading the plugin's <c>theme.json</c> off disk on every request.
/// </para>
/// </summary>
public sealed class WorkspaceSectionLayoutDefinition
{
    public Guid Id { get; set; }

    public string LayoutKey { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The regions, as a JSON array of <c>{ "key": …, "label": … }</c>.
    /// <para>
    /// One column rather than a second table: regions are never asked for without their layout,
    /// and nothing queries across them. A table would buy a join and an ordering column for a
    /// list that is read whole or not at all.
    /// </para>
    /// </summary>
    public string RegionsJson { get; set; } = "[]";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
