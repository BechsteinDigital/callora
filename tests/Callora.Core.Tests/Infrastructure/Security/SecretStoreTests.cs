using Callora.Core.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Security;

public sealed class SecretStoreTests
{
    [Fact]
    public async Task EnvironmentSecretStore_ReadsPrefixedVariable_WithNormalizedName()
    {
        // Inject the environment reader instead of mutating a process-global variable,
        // which would pollute config-reading tests running in parallel. This still
        // exercises the CALLORA_SECRET_<NAME> prefixing + name normalization.
        var store = new EnvironmentSecretStore(variableName =>
            variableName == "CALLORA_SECRET_MARKETPLACE_API_KEY" ? "env-value" : null);

        Assert.Equal("env-value", await store.GetSecretAsync("marketplace-api.key"));
        Assert.Null(await store.GetSecretAsync("unknown"));
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
