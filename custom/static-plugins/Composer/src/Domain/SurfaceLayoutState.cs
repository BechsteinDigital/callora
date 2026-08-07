namespace Callora.Plugin.Composer.Domain;

/// <summary>
/// Where a version stands. Exactly one draft and at most one published version per layout; the
/// rest is history.
/// </summary>
public enum SurfaceLayoutState
{
    /// <summary>
    /// The working copy. Autosave writes here and creates NO version — only publishing does.
    /// Nothing on a surface ever renders from this.
    /// </summary>
    Draft = 0,

    /// <summary>The one version the public render path may see.</summary>
    Published = 1,

    /// <summary>A former publication, kept so a rollback is copying a row.</summary>
    Archived = 2,
}
