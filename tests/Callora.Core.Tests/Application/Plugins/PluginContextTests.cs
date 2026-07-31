using Callora.Core.Application.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

public sealed class PluginContextTests
{
    [Fact]
    public void PluginConfiguration_ExposesOnlyTheOwningPluginSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["communication:WebRtc:Enabled"] = "true",
                ["BackendHost:JwtSigningKey"] = "must-stay-hidden",
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();
        var context = new PluginContext(
            services,
            "communication",
            (_, _, _) => { },
            _ => null);

        Assert.NotNull(context.PluginConfiguration);
        Assert.Equal("true", context.PluginConfiguration!["WebRtc:Enabled"]);
        Assert.Null(context.PluginConfiguration["BackendHost:JwtSigningKey"]);
    }
}
