namespace Callora.Core.Application.Extensions;

/// <summary>
/// Persistence for the section layouts a theme plugin declares.
/// <para>
/// Definitions only — there is nothing an operator chooses here. Which layout a section uses is
/// part of the layout document, not a stored setting, so this store has no value side and no
/// workspace/surface cascade.
/// </para>
/// </summary>
public interface IWorkspaceSectionLayoutStore
{
    /// <summary>The active layouts of one theme version, in declared order.</summary>
    Task<IReadOnlyList<SectionLayoutDefinition>> ListAsync(
        string pluginId,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces this plugin version's layouts with what its <c>theme.json</c> now declares.
    /// <para>
    /// Replace rather than merge: a layout the theme dropped must disappear, or the editor would
    /// keep offering a grid nothing can style. Documents that still name it are not touched —
    /// they fall back at render time (§7.8) and become whole again if the theme brings it back.
    /// </para>
    /// </summary>
    Task ReplaceForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<SectionLayoutDefinition> layouts,
        CancellationToken cancellationToken = default);

    Task ClearForPluginAsync(string pluginId, CancellationToken cancellationToken = default);
}
