using Callora.Plugin.Composer.Domain;

namespace Callora.Plugin.Composer.Domain;

/// <summary>
/// One version of a layout: an immutable JSON document plus where it stands.
/// <para>
/// The document is stored whole rather than normalised into sections and blocks. A version is a
/// snapshot and is never partially changed — rolling back is copying a row, a diff is trivial,
/// and the renderer reads one document instead of three joins.
/// </para>
/// </summary>
public sealed class SurfaceLayoutVersion
{
    // EF materialises through this; everything else goes through the factory below, so a version
    // cannot exist without the fields that give it meaning.
    private SurfaceLayoutVersion()
    {
        LayoutKey = string.Empty;
        Document = string.Empty;
    }

    private SurfaceLayoutVersion(
        Guid id,
        string layoutKey,
        int versionNumber,
        SurfaceLayoutState state,
        string document,
        string? label,
        string createdBy,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        LayoutKey = layoutKey;
        VersionNumber = versionNumber;
        State = state;
        Document = document;
        Label = label;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
        ChangedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string LayoutKey { get; private set; }

    /// <summary>Ascending, and only publishing hands one out. A draft carries the next number.</summary>
    public int VersionNumber { get; private set; }

    public SurfaceLayoutState State { get; private set; }

    /// <summary>The layout document as JSON.</summary>
    public string Document { get; private set; }

    /// <summary>What the editor called this publication, if anything.</summary>
    public string? Label { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Last write. The editor sends it back on save; a second save from a stale editor is
    /// refused rather than silently overwriting the first.
    /// </summary>
    public DateTimeOffset ChangedAtUtc { get; private set; }

    public string? PublishedBy { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    /// <summary>A new draft, the first version of a layout or the successor to a publication.</summary>
    public static SurfaceLayoutVersion NewDraft(
        string layoutKey,
        int versionNumber,
        string document,
        string createdBy,
        DateTimeOffset utcNow) =>
        new(Guid.NewGuid(), layoutKey, versionNumber, SurfaceLayoutState.Draft, document, null, createdBy, utcNow);

    /// <summary>Replaces the draft's content. Autosave — no new version, no history entry.</summary>
    public void UpdateDocument(string document, DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document);
        Document = document;
        ChangedAtUtc = utcNow;
    }

    /// <summary>Makes this draft the published version.</summary>
    public void Publish(string publishedBy, string? label, DateTimeOffset utcNow)
    {
        State = SurfaceLayoutState.Published;
        Label = label;
        PublishedBy = publishedBy;
        PublishedAtUtc = utcNow;
        ChangedAtUtc = utcNow;
    }

    /// <summary>Retires a former publication.</summary>
    public void Archive(DateTimeOffset utcNow)
    {
        State = SurfaceLayoutState.Archived;
        ChangedAtUtc = utcNow;
    }
}
