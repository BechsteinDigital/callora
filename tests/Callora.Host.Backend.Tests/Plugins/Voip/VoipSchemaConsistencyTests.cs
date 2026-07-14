using System.Text.Json;
using Callora.Host.Backend.Infrastructure.Persistence;
using Callora.Host.Backend.Tests.Cli;
using Callora.Plugins.Voip.Application;
using Callora.Plugins.Voip.Application.Persistence;

namespace Callora.Host.Backend.Tests.Plugins.Voip;

/// <summary>
/// Guards against the three schema-name sources drifting apart (review
/// finding, PLAT-260): the DbContext schema, the plugin_&lt;id&gt; convention
/// and the manifest's databaseSchema field must all agree, so the uninstall
/// cleanup drops exactly the schema the plugin actually uses.
/// </summary>
public sealed class VoipSchemaConsistencyTests
{
    [Fact]
    public void DbContextSchema_MatchesConvention()
    {
        Assert.Equal(VoipDbContext.SchemaName, PluginSchemaName.TryResolve(VoipPlugin.Id));
    }

    [Fact]
    public void ManifestDatabaseSchema_MatchesDbContextSchema()
    {
        var repoRoot = ScaffoldedPluginFixture.ResolveRepositoryRoot();
        var manifestPath = Path.Combine(repoRoot, "custom", "plugins", "Voip", "registry.json");

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var declared = document.RootElement.GetProperty("databaseSchema").GetString();

        Assert.Equal(VoipDbContext.SchemaName, declared);
    }
}
