using Callora.Plugins.Voip.Application.Accounts;

namespace Callora.Host.Backend.Tests.Plugins.Voip;

public sealed class SipAccountJsonbImporterTests
{
    private static SipAccountEntry Entry(string id) =>
        new(id, id, "example.org", id, "secret", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Import_MovesAccountsToTarget_AndClearsLegacy()
    {
        var legacy = new InMemorySipAccountStore();
        var target = new InMemorySipAccountStore();
        legacy.Seed("workspace-a", Entry("alice-example.org"));
        legacy.Seed("workspace-a", Entry("bob-example.org"));

        await new SipAccountJsonbImporter(legacy, target).ImportAsync();

        Assert.Equal(2, (await target.ListAsync("workspace-a")).Count);
        Assert.Empty(await legacy.ListAsync("workspace-a"));
    }

    [Fact]
    public async Task Import_IsIdempotent_SkipsAlreadyImportedAccounts()
    {
        var legacy = new InMemorySipAccountStore();
        var target = new InMemorySipAccountStore();
        var entry = Entry("alice-example.org");
        legacy.Seed("workspace-a", entry);
        target.Seed("workspace-a", entry); // already imported

        await new SipAccountJsonbImporter(legacy, target).ImportAsync();

        Assert.Equal(0, target.CreateCount);           // not created again
        Assert.Empty(await legacy.ListAsync("workspace-a")); // legacy copy removed
    }
}
