using Callora.Core.Application.Data.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Support;
using Callora.Plugin.Communication.Application.Accounts;
using Xunit;

namespace Callora.Core.Tests.Plugins.Voip;

public sealed class SipAccountSecretProtectionTests
{
    [Fact]
    public async Task Secret_IsNotStoredInPlaintext_AtRest()
    {
        var dataStore = new InMemoryPluginDataStore();
        var store = new DataStoreSipAccountStore(dataStore, new FakePluginDataProtector());

        var created = await store.CreateAsync("workspace-a", NewRequest("alice", secret: "super-geheim"));

        var rawJson = await dataStore.GetAsync(new PluginDataKey("communication", "workspace-a", "sip-accounts", created.SipAccountId));
        Assert.NotNull(rawJson);
        using var document = System.Text.Json.JsonDocument.Parse(rawJson!);
        var storedSecret = document.RootElement.GetProperty("secret").GetString();
        Assert.NotEqual("super-geheim", storedSecret);
        Assert.StartsWith("protected:communication:", storedSecret);
    }

    [Fact]
    public async Task Secret_RoundtripsThroughEncryption()
    {
        var store = new DataStoreSipAccountStore(new InMemoryPluginDataStore(), new FakePluginDataProtector());

        var created = await store.CreateAsync("workspace-a", NewRequest("alice", secret: "super-geheim"));

        var loaded = await store.GetAsync("workspace-a", created.SipAccountId);
        Assert.Equal("super-geheim", loaded!.Secret);
        var listed = Assert.Single(await store.ListAsync("workspace-a"));
        Assert.Equal("super-geheim", listed.Secret);
    }

    [Fact]
    public async Task LegacyPlaintextSecret_StaysReadable()
    {
        var dataStore = new InMemoryPluginDataStore();
        // Alt-Datensatz simulieren: Secret liegt unverschlüsselt im Dokument.
        await dataStore.SetAsync(
            new PluginDataKey("communication", "workspace-a", "sip-accounts", "legacy-acc"),
            """{"sipAccountId":"legacy-acc","username":"old","domain":"voice.example.org","displayName":"Old","secret":"klartext","isActive":true,"createdAtUtc":"2026-01-01T00:00:00+00:00","updatedAtUtc":"2026-01-01T00:00:00+00:00"}""");
        var store = new DataStoreSipAccountStore(dataStore, new FakePluginDataProtector());

        var loaded = await store.GetAsync("workspace-a", "legacy-acc");

        Assert.NotNull(loaded);
        Assert.Equal("klartext", loaded!.Secret);
    }

    private static UpsertSipAccountRequest NewRequest(string username, string secret) =>
        new(
            Username: username,
            Domain: "voice.example.org",
            DisplayName: $"{username} Display",
            Secret: secret,
            IsActive: true);
}
