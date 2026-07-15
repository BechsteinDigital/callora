using System.Text;

namespace Callora.Core.Api;

/// <summary>
/// Cursor pagination over an ordered, already-filtered list (PLAT-211).
/// The cursor is the base64-encoded stable key of the last returned item;
/// an unknown cursor (item deleted meanwhile) restarts from the beginning.
/// Paging happens after the store query today — the API contract stays
/// stable when stores later move to keyset queries.
/// </summary>
public static class ListPagination
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static PagedApiResponse<T> Page<T>(
        IReadOnlyList<T> orderedItems,
        int? limit,
        string? cursor,
        Func<T, string> cursorKeySelector)
    {
        ArgumentNullException.ThrowIfNull(orderedItems);
        ArgumentNullException.ThrowIfNull(cursorKeySelector);

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        var startIndex = 0;
        if (TryDecode(cursor, out var cursorKey))
        {
            for (var index = 0; index < orderedItems.Count; index++)
            {
                if (string.Equals(cursorKeySelector(orderedItems[index]), cursorKey, StringComparison.Ordinal))
                {
                    startIndex = index + 1;
                    break;
                }
            }
        }

        var page = orderedItems.Skip(startIndex).Take(effectiveLimit).ToArray();
        var hasMore = startIndex + page.Length < orderedItems.Count;
        var nextCursor = hasMore && page.Length > 0
            ? Encode(cursorKeySelector(page[^1]))
            : null;

        return new PagedApiResponse<T>(page, orderedItems.Count, nextCursor);
    }

    private static string Encode(string key) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(key));

    private static bool TryDecode(string? cursor, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            key = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return key.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
