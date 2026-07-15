using Callora.Host.Backend.Application.Plugins;
using Callora.Host.Backend.Tests.Support;
using Callora.Plugin.Communication.Application.Accounts;
using Xunit;

namespace Callora.Host.Backend.Tests.Plugins.Voip;

public sealed class DataStoreSipAccountStoreTests
{
    [Fact]
    public async Task CreateAndGet_RoundtripsAccount()
    {
        var store = CreateStore();

        var created = await store.CreateAsync("workspace-a", NewRequest("alice"));

        var loaded = await store.GetAsync("workspace-a", created.SipAccountId);
        Assert.NotNull(loaded);
        Assert.Equal("alice", loaded!.Username);
        Assert.Equal("voice.example.org", loaded.Domain);
        Assert.True(loaded.IsActive);
    }

    [Fact]
    public async Task Create_DuplicateAccount_Throws()
    {
        var store = CreateStore();
        await store.CreateAsync("workspace-a", NewRequest("alice"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateAsync("workspace-a", NewRequest("alice")));
    }

    [Fact]
    public async Task Accounts_AreIsolatedPerWorkspace()
    {
        var store = CreateStore();
        await store.CreateAsync("workspace-a", NewRequest("alice"));

        Assert.Single(await store.ListAsync("workspace-a"));
        Assert.Empty(await store.ListAsync("workspace-b"));
    }

    [Fact]
    public async Task Update_ReplacesFields_AndKeepsId()
    {
        var store = CreateStore();
        var created = await store.CreateAsync("workspace-a", NewRequest("alice"));

        var updated = await store.UpdateAsync(
            "workspace-a",
            created.SipAccountId,
            NewRequest("alice") with { DisplayName = "Alice Neu", IsActive = false });

        Assert.NotNull(updated);
        Assert.Equal(created.SipAccountId, updated!.SipAccountId);
        Assert.Equal("Alice Neu", updated.DisplayName);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Delete_RemovesAccount()
    {
        var store = CreateStore();
        var created = await store.CreateAsync("workspace-a", NewRequest("alice"));

        Assert.True(await store.DeleteAsync("workspace-a", created.SipAccountId));
        Assert.False(await store.DeleteAsync("workspace-a", created.SipAccountId));
        Assert.Null(await store.GetAsync("workspace-a", created.SipAccountId));
    }

    [Fact]
    public async Task ListWorkspaceKeys_ReturnsWorkspacesWithAccounts()
    {
        var store = CreateStore();
        await store.CreateAsync("workspace-a", NewRequest("alice"));
        await store.CreateAsync("workspace-b", NewRequest("bob"));

        var workspaceKeys = await store.ListWorkspaceKeysAsync();

        Assert.Equal(["workspace-a", "workspace-b"], workspaceKeys);
    }

    private static DataStoreSipAccountStore CreateStore() =>
        new(new InMemoryPluginDataStore(), new FakePluginDataProtector());

    private static UpsertSipAccountRequest NewRequest(string username) =>
        new(
            Username: username,
            Domain: "voice.example.org",
            DisplayName: $"{username} Display",
            Secret: "secret",
            IsActive: true);
}
