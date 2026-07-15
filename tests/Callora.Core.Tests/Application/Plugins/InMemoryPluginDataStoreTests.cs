using Callora.Core.Application.Plugins;
using Callora.Core.Application.Data.Contracts;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

public sealed class InMemoryPluginDataStoreTests
{
    [Fact]
    public async Task SetAndGet_RoundtripsDocument()
    {
        var store = new InMemoryPluginDataStore();
        var key = new PluginDataKey("voice", "workspace-a", "sip-accounts", "acc-1");

        await store.SetAsync(key, """{"username":"alice"}""");

        Assert.Equal("""{"username":"alice"}""", await store.GetAsync(key));
    }

    [Fact]
    public async Task Get_UnknownKey_ReturnsNull()
    {
        var store = new InMemoryPluginDataStore();

        var document = await store.GetAsync(new PluginDataKey("voice", "workspace-a", "sip-accounts", "missing"));

        Assert.Null(document);
    }

    [Fact]
    public async Task Set_OverwritesExistingDocument()
    {
        var store = new InMemoryPluginDataStore();
        var key = new PluginDataKey("voice", "workspace-a", "sip-accounts", "acc-1");
        await store.SetAsync(key, """{"v":1}""");

        await store.SetAsync(key, """{"v":2}""");

        Assert.Equal("""{"v":2}""", await store.GetAsync(key));
    }

    [Fact]
    public async Task Documents_AreIsolatedByWorkspaceAndPlugin()
    {
        var store = new InMemoryPluginDataStore();
        await store.SetAsync(new PluginDataKey("voice", "workspace-a", "col", "k"), """{"scope":"a"}""");
        await store.SetAsync(new PluginDataKey("voice", "workspace-b", "col", "k"), """{"scope":"b"}""");
        await store.SetAsync(new PluginDataKey("dialer", "workspace-a", "col", "k"), """{"scope":"dialer"}""");
        await store.SetAsync(new PluginDataKey("voice", null, "col", "k"), """{"scope":"global"}""");

        Assert.Equal("""{"scope":"a"}""", await store.GetAsync(new PluginDataKey("voice", "workspace-a", "col", "k")));
        Assert.Equal("""{"scope":"b"}""", await store.GetAsync(new PluginDataKey("voice", "workspace-b", "col", "k")));
        Assert.Equal("""{"scope":"dialer"}""", await store.GetAsync(new PluginDataKey("dialer", "workspace-a", "col", "k")));
        Assert.Equal("""{"scope":"global"}""", await store.GetAsync(new PluginDataKey("voice", null, "col", "k")));
    }

    [Fact]
    public async Task List_ReturnsCollectionEntriesOrderedByKey()
    {
        var store = new InMemoryPluginDataStore();
        await store.SetAsync(new PluginDataKey("voice", "workspace-a", "sip-accounts", "b"), """{"n":2}""");
        await store.SetAsync(new PluginDataKey("voice", "workspace-a", "sip-accounts", "a"), """{"n":1}""");
        await store.SetAsync(new PluginDataKey("voice", "workspace-a", "other", "c"), """{"n":3}""");

        var entries = await store.ListAsync(new PluginDataCollectionKey("voice", "workspace-a", "sip-accounts"));

        Assert.Equal(2, entries.Count);
        Assert.Equal("a", entries[0].EntryKey);
        Assert.Equal("b", entries[1].EntryKey);
    }

    [Fact]
    public async Task Remove_DeletesDocument_AndReportsExistence()
    {
        var store = new InMemoryPluginDataStore();
        var key = new PluginDataKey("voice", "workspace-a", "sip-accounts", "acc-1");
        await store.SetAsync(key, "{}");

        Assert.True(await store.RemoveAsync(key));
        Assert.False(await store.RemoveAsync(key));
        Assert.Null(await store.GetAsync(key));
    }
}
