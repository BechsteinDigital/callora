namespace Callora.Plugin.Composer.Domain;

/// <summary>
/// The state machine of a layout's versions, as pure functions over the versions it is given.
/// <para>
/// Separated from persistence on purpose: these rules are the part worth testing, and they should
/// not need a database to be tested. The store loads, calls in here, and saves.
/// </para>
/// <para>
/// Four transitions, and each one is a decision:
/// <list type="bullet">
/// <item><b>Publish</b> makes the draft the published version and archives the previous one, then
/// starts a fresh draft from it. Without that last step the next edit would have nothing to write
/// into.</item>
/// <item><b>Discard</b> rebuilds the draft from what is live — the only way back that cannot lose
/// something somebody published.</item>
/// <item><b>Roll back</b> copies an archived version INTO THE DRAFT, never straight to live. A
/// rollback is a proposal like any other edit; making it live directly would be the one path that
/// skips looking at the result.</item>
/// <item><b>Autosave</b> creates no version at all. Only publishing does — otherwise history is a
/// log of keystrokes and rolling back means guessing which of four hundred entries was meant.</item>
/// </list>
/// </para>
/// </summary>
public static class SurfaceLayoutTransitions
{
    /// <summary>
    /// Publishes <paramref name="draft"/>. The previous publication is archived; a new draft
    /// continues from the published content so the next edit has somewhere to go.
    /// </summary>
    /// <returns>The new draft that takes over editing.</returns>
    public static SurfaceLayoutVersion Publish(
        SurfaceLayoutVersion draft,
        SurfaceLayoutVersion? currentlyPublished,
        string publishedBy,
        string? label,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Require(draft, SurfaceLayoutState.Draft, nameof(draft));

        currentlyPublished?.Archive(utcNow);
        draft.Publish(publishedBy, label, utcNow);

        return SurfaceLayoutVersion.NewDraft(
            draft.LayoutKey, draft.VersionNumber + 1, draft.Document, publishedBy, utcNow);
    }

    /// <summary>
    /// Throws the draft's changes away and rebuilds it from the published version — or, when
    /// nothing was ever published, from an empty document. Discarding must not leave a layout
    /// without a draft to edit.
    /// </summary>
    public static SurfaceLayoutVersion Discard(
        SurfaceLayoutVersion draft,
        SurfaceLayoutVersion? currentlyPublished,
        string emptyDocument,
        string discardedBy,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Require(draft, SurfaceLayoutState.Draft, nameof(draft));

        draft.UpdateDocument(currentlyPublished?.Document ?? emptyDocument, utcNow);
        return draft;
    }

    /// <summary>
    /// Takes an archived version's content into the draft. Deliberately not straight to live: a
    /// rollback is a proposal, and the person doing it should see it before a visitor does.
    /// </summary>
    public static SurfaceLayoutVersion RollBack(
        SurfaceLayoutVersion draft,
        SurfaceLayoutVersion archived,
        string rolledBackBy,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(archived);
        Require(draft, SurfaceLayoutState.Draft, nameof(draft));

        if (archived.State == SurfaceLayoutState.Draft)
        {
            throw new InvalidOperationException("A draft cannot be rolled back to; it is already the draft.");
        }

        draft.UpdateDocument(archived.Document, utcNow);
        return draft;
    }

    /// <summary>
    /// Writes into the draft, refusing a write that was composed against an older state.
    /// <para>
    /// Optimistic, not locked: a lock would have to be released by an editor that may simply have
    /// closed its tab. The second writer gets a conflict and can decide; silently overwriting
    /// would lose work without anyone noticing, which is the outcome worth avoiding.
    /// </para>
    /// </summary>
    /// <returns>False when <paramref name="expectedChangedAtUtc"/> no longer matches.</returns>
    public static bool TryAutosave(
        SurfaceLayoutVersion draft,
        string document,
        DateTimeOffset expectedChangedAtUtc,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Require(draft, SurfaceLayoutState.Draft, nameof(draft));

        if (draft.ChangedAtUtc != expectedChangedAtUtc)
        {
            return false;
        }

        draft.UpdateDocument(document, utcNow);
        return true;
    }

    private static void Require(SurfaceLayoutVersion version, SurfaceLayoutState expected, string parameter)
    {
        if (version.State != expected)
        {
            throw new ArgumentException(
                $"Expected a {expected} version, got {version.State}.", parameter);
        }
    }
}
