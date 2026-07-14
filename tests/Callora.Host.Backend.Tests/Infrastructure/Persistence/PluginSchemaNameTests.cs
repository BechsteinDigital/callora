using Callora.Host.Backend.Infrastructure.Persistence;

namespace Callora.Host.Backend.Tests.Infrastructure.Persistence;

public sealed class PluginSchemaNameTests
{
    [Theory]
    [InlineData("voip", "plugin_voip")]
    [InlineData("acme-dialer", "plugin_acme_dialer")]
    [InlineData("VOIP", "plugin_voip")]
    public void TryResolve_ValidIds_ProducePrefixedSchema(string pluginId, string expected)
    {
        Assert.Equal(expected, PluginSchemaName.TryResolve(pluginId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad name")]
    [InlineData("drop;table")]
    [InlineData("\"inject\"")]
    [InlineData("1leading")]
    public void TryResolve_UnsafeIds_ReturnNull(string pluginId)
    {
        Assert.Null(PluginSchemaName.TryResolve(pluginId));
    }
}
