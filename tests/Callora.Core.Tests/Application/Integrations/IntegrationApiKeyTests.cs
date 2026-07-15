using Callora.Core.Application.Integrations;
using Xunit;

namespace Callora.Core.Tests.Application.Integrations;

public sealed class IntegrationApiKeyTests
{
    [Fact]
    public void Generate_ProducesPrefixedHighEntropyKey()
    {
        var key = IntegrationApiKey.Generate();

        Assert.StartsWith(IntegrationApiKey.Prefix, key, StringComparison.Ordinal);
        Assert.True(key.Length > IntegrationApiKey.Prefix.Length + 20);
    }

    [Fact]
    public void Generate_ProducesUniqueKeys()
    {
        Assert.NotEqual(IntegrationApiKey.Generate(), IntegrationApiKey.Generate());
    }

    [Fact]
    public void ComputeHash_IsDeterministicAndKeyDependent()
    {
        var key = IntegrationApiKey.Generate();

        Assert.Equal(IntegrationApiKey.ComputeHash(key), IntegrationApiKey.ComputeHash(key));
        Assert.NotEqual(IntegrationApiKey.ComputeHash(key), IntegrationApiKey.ComputeHash(key + "x"));
    }

    [Fact]
    public void ComputeHash_TrimsSurroundingWhitespace()
    {
        var key = IntegrationApiKey.Generate();

        Assert.Equal(IntegrationApiKey.ComputeHash(key), IntegrationApiKey.ComputeHash($"  {key}  "));
    }

    [Fact]
    public void DerivePrefix_DoesNotRevealWholeKey()
    {
        var key = IntegrationApiKey.Generate();

        var prefix = IntegrationApiKey.DerivePrefix(key);

        Assert.True(prefix.Length < key.Length);
        Assert.StartsWith(prefix, key, StringComparison.Ordinal);
    }
}
