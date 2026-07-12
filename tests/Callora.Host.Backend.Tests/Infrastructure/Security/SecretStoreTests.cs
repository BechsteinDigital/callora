using Callora.Host.Backend.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Callora.Host.Backend.Tests.Infrastructure.Security;

public sealed class SecretStoreTests
{
    [Fact]
    public async Task EnvironmentSecretStore_ReadsPrefixedVariable_WithNormalizedName()
    {
        Environment.SetEnvironmentVariable("CALLORA_SECRET_MARKETPLACE_API_KEY", "env-value");
        try
        {
            var store = new EnvironmentSecretStore();

            Assert.Equal("env-value", await store.GetSecretAsync("marketplace-api.key"));
            Assert.Null(await store.GetSecretAsync("unknown"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CALLORA_SECRET_MARKETPLACE_API_KEY", null);
        }
    }

    [Fact]
    public async Task ConfigurationSecretStore_ReadsSecretsSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:signing-key"] = "config-value"
            })
            .Build();
        var store = new ConfigurationSecretStore(configuration);

        Assert.Equal("config-value", await store.GetSecretAsync("signing-key"));
        Assert.Null(await store.GetSecretAsync("missing"));
    }

    [Fact]
    public async Task ChainedSecretStore_FirstNonNullWins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:only-config"] = "config-value"
            })
            .Build();
        var chain = new ChainedSecretStore(
        [
            new EnvironmentSecretStore(),
            new ConfigurationSecretStore(configuration)
        ]);

        Assert.Equal("config-value", await chain.GetSecretAsync("only-config"));
        Assert.Null(await chain.GetSecretAsync("nowhere"));
    }
}
