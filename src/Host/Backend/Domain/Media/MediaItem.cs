namespace Callora.Host.Backend.Domain.Media;

/// <summary>
/// Metadata of one stored media asset (announcement audio, logos, …); the
/// bytes live in the media storage under the item id.
/// </summary>
public sealed class MediaItem
{
    public Guid Id { get; set; }

    public string WorkspaceKey { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>Logical folder, e.g. "announcements" or "branding".</summary>
    public string Folder { get; set; } = string.Empty;

    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
