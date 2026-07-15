using Callora.Core.Api;

namespace Callora.Core.Tests.Api;

public sealed class ListPaginationTests
{
    [Fact]
    public void Page_WalksAllItemsViaCursor_WithoutDuplicatesOrGaps()
    {
        var items = Enumerable.Range(1, 7).Select(static i => $"item-{i}").ToArray();

        var first = ListPagination.Page(items, limit: 3, cursor: null, static x => x);
        Assert.Equal(["item-1", "item-2", "item-3"], first.Items);
        Assert.Equal(7, first.Total);
        Assert.NotNull(first.NextCursor);

        var second = ListPagination.Page(items, limit: 3, first.NextCursor, static x => x);
        Assert.Equal(["item-4", "item-5", "item-6"], second.Items);
        Assert.NotNull(second.NextCursor);

        var third = ListPagination.Page(items, limit: 3, second.NextCursor, static x => x);
        Assert.Equal(["item-7"], third.Items);
        Assert.Null(third.NextCursor);
    }

    [Fact]
    public void Page_UnknownOrMalformedCursor_RestartsFromBeginning()
    {
        var items = new[] { "a", "b" };

        var unknown = ListPagination.Page(items, limit: 10, cursor: Convert.ToBase64String("zz"u8.ToArray()), static x => x);
        Assert.Equal(items, unknown.Items);

        var malformed = ListPagination.Page(items, limit: 10, cursor: "not-base64!!", static x => x);
        Assert.Equal(items, malformed.Items);
    }

    [Fact]
    public void Page_ClampsLimit()
    {
        var items = Enumerable.Range(1, 300).Select(static i => i.ToString()).ToArray();

        var oversized = ListPagination.Page(items, limit: 999, cursor: null, static x => x);
        Assert.Equal(ListPagination.MaxLimit, oversized.Items.Count);

        var undersized = ListPagination.Page(items, limit: 0, cursor: null, static x => x);
        Assert.Single(undersized.Items);
    }
}
