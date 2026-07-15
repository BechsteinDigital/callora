namespace Callora.Core.Application.Media;

/// <summary>
/// Upload constraints: content-type whitelist (audio for announcements,
/// images for branding) and a hard size cap.
/// </summary>
public static class MediaUploadPolicy
{
    public const long MaxSizeBytes = 25 * 1024 * 1024;

    // SVG is deliberately absent: script-capable markup served from the
    // API origin would be a stored-XSS vector.
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/wav",
        "audio/x-wav",
        "audio/mpeg",
        "audio/ogg",
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    public static bool IsAllowedContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && AllowedContentTypes.Contains(contentType.Trim());

    public static bool IsAllowedSize(long sizeBytes) =>
        sizeBytes > 0 && sizeBytes <= MaxSizeBytes;
}
