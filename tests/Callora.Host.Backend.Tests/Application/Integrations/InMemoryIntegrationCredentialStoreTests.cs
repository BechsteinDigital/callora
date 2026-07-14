using Callora.Host.Backend.Application.Integrations;
using Callora.Host.Backend.Domain.Integrations;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Integrations;

public sealed class InMemoryIntegrationCredentialStoreTests
{
    [Fact]
    public async Task Add_Then_FindActiveByKeyHash_ReturnsCredential()
    {
        var store = new InMemoryIntegrationCredentialStore();
        var credential = Create("billing", "KEYHASH");
        await store.AddAsync(credential);

        var found = await store.FindActiveByKeyHashAsync("KEYHASH");

        Assert.NotNull(found);
        Assert.Equal("billing", found!.Name);
    }

    [Fact]
    public async Task FindActiveByKeyHash_IgnoresRevoked()
    {
        var store = new InMemoryIntegrationCredentialStore();
        var credential = Create("billing", "KEYHASH");
        await store.AddAsync(credential);
        await store.RevokeAsync(credential.Id);

        Assert.Null(await store.FindActiveByKeyHashAsync("KEYHASH"));
    }

    [Fact]
    public async Task Revoke_Twice_SecondReturnsFalse()
    {
        var store = new InMemoryIntegrationCredentialStore();
        var credential = Create("billing", "KEYHASH");
        await store.AddAsync(credential);

        Assert.True(await store.RevokeAsync(credential.Id));
        Assert.False(await store.RevokeAsync(credential.Id));
    }

    [Fact]
    public async Task ExistsByName_IsCaseInsensitive()
    {
        var store = new InMemoryIntegrationCredentialStore();
        await store.AddAsync(Create("Billing", "KEYHASH"));

        Assert.True(await store.ExistsByNameAsync("billing"));
        Assert.False(await store.ExistsByNameAsync("other"));
    }

    private static IntegrationCredential Create(string name, string keyHash) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        KeyHash = keyHash,
        KeyPrefix = "clra_xxxxxxx",
        RoleName = "billing-role",
        Scope = "platform",
        IsActive = true,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };
}
