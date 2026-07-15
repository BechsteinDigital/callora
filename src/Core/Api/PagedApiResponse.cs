namespace Callora.Core.Api;

/// <summary>
/// Envelope for paginated list responses (PLAT-211): the page items, the
/// total match count and an opaque cursor for the next page (null on the
/// last page).
/// </summary>
public sealed record PagedApiResponse<T>(
    IReadOnlyList<T> Items,
    int Total,
    string? NextCursor);
