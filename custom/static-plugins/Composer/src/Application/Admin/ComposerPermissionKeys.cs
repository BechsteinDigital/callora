namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// What an operator must hold to use the Composer.
/// <para>
/// Reading and publishing are separate on purpose. A draft is not yet anybody's decision — it can
/// be wrong, half-finished, or an experiment. Publishing puts it in front of visitors, and the two
/// are different enough that one person may reasonably do the first and not the second.
/// </para>
/// </summary>
public static class ComposerPermissionKeys
{
    /// <summary>See layouts and their drafts.</summary>
    public const string LayoutRead = "composer.layout.read";

    /// <summary>Create layouts and write into their drafts.</summary>
    public const string LayoutWrite = "composer.layout.write";

    /// <summary>Make a draft live, discard it, or roll one back.</summary>
    public const string LayoutPublish = "composer.layout.publish";

    /// <summary>All of them, for the host's permission catalogue.</summary>
    public static readonly IReadOnlyList<string> All = [LayoutRead, LayoutWrite, LayoutPublish];
}
