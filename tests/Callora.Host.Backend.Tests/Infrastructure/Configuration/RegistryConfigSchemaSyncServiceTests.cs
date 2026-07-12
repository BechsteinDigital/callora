using Callora.Host.Backend.Infrastructure.Configuration;
using Xunit;

namespace Callora.Host.Backend.Tests.Infrastructure.Configuration;

public sealed class RegistryConfigSchemaSyncServiceTests
{
    [Fact]
    public void ParseConfigFields_ReadsFieldsWithDefaultsGroupsAndOrder()
    {
        const string registryJson = """
        {
          "pluginId": "voip",
          "config": {
            "fields": {
              "codec.preferred": { "label": "Bevorzugter Codec", "type": "select", "default": "PCMA", "group": "media", "options": ["PCMA", "PCMU", "G722"] },
              "srtp.enabled": { "label": "SRTP verwenden", "type": "bool", "default": false, "order": 5 }
            }
          }
        }
        """;

        var definitions = RegistryConfigSchemaSyncService.ParseConfigFields(registryJson);

        Assert.Equal(2, definitions.Count);
        var codec = definitions[0];
        Assert.Equal("codec.preferred", codec.ConfigKey);
        Assert.Equal("Bevorzugter Codec", codec.Label);
        Assert.Equal("select", codec.FieldType);
        Assert.Equal("\"PCMA\"", codec.DefaultValueJson);
        Assert.Equal("media", codec.GroupName);
        Assert.NotNull(codec.OptionsJson);
        Assert.Equal(10, codec.SortOrder);

        var srtp = definitions[1];
        Assert.Equal("false", srtp.DefaultValueJson);
        Assert.Equal(5, srtp.SortOrder);
        Assert.True(srtp.IsActive);
    }

    [Fact]
    public void ParseConfigFields_WithoutConfigSection_ReturnsEmpty()
    {
        Assert.Empty(RegistryConfigSchemaSyncService.ParseConfigFields("""{ "pluginId": "voip" }"""));
    }
}
