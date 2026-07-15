using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Infrastructure.Persistence;

public sealed class PluginManifestSchemaReaderTests
{
    [Fact]
    public void ReadsDeclaredSchema_WhenPresentAndSafe()
    {
        var assemblyPath = WriteManifest("""{ "pluginId": "voip", "databaseSchema": "plugin_voip" }""");
        Assert.Equal("plugin_voip", PluginManifestSchemaReader.TryReadDatabaseSchema(assemblyPath));
    }

    [Fact]
    public void ReturnsNull_WhenFieldMissing()
    {
        var assemblyPath = WriteManifest("""{ "pluginId": "voip" }""");
        Assert.Null(PluginManifestSchemaReader.TryReadDatabaseSchema(assemblyPath));
    }

    [Fact]
    public void ReturnsNull_WhenManifestMissing()
    {
        using var workspace = new TempWorkspace();
        var pluginDir = workspace.CreateDirectory("plugin");
        var assemblyPath = Path.Combine(pluginDir, "plugin.dll");
        File.WriteAllText(assemblyPath, "stub"); // no registry.json next to it

        Assert.Null(PluginManifestSchemaReader.TryReadDatabaseSchema(assemblyPath));
    }

    [Fact]
    public void ReturnsNull_OnBrokenJson()
    {
        var assemblyPath = WriteManifest("{ not valid json ");
        Assert.Null(PluginManifestSchemaReader.TryReadDatabaseSchema(assemblyPath));
    }

    [Fact]
    public void ReturnsNull_WhenDeclaredSchemaIsUnsafe()
    {
        var assemblyPath = WriteManifest("""{ "databaseSchema": "drop table" }""");
        Assert.Null(PluginManifestSchemaReader.TryReadDatabaseSchema(assemblyPath));
    }

    private static string WriteManifest(string manifestJson)
    {
        var workspace = new TempWorkspace();
        var pluginDir = workspace.CreateDirectory("plugin");
        var assemblyPath = Path.Combine(pluginDir, "plugin.dll");
        File.WriteAllText(assemblyPath, "stub");
        File.WriteAllText(Path.Combine(pluginDir, "registry.json"), manifestJson);
        return assemblyPath;
    }
}
