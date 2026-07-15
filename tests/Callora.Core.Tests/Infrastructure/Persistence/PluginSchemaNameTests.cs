using Callora.Core.Infrastructure.Persistence;

namespace Callora.Core.Tests.Infrastructure.Persistence;

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

    [Theory]
    [InlineData("plugin_voip", "plugin_voip")]
    [InlineData("PLUGIN_VOIP", "plugin_voip")]
    public void Sanitize_ValidSchemaNames_PassThroughNormalized(string declared, string expected)
    {
        Assert.Equal(expected, PluginSchemaName.Sanitize(declared));
    }

    [Theory]
    [InlineData("drop table")]
    [InlineData("a;b")]
    [InlineData("\"x\"")]
    [InlineData("9start")]
    [InlineData(null)]
    public void Sanitize_UnsafeSchemaNames_ReturnNull(string? declared)
    {
        Assert.Null(PluginSchemaName.Sanitize(declared));
    }
}
