using Callora.Core.Application.Plugins;
using Callora.Core.Infrastructure.Plugins;
using System.Text;

namespace Callora.Core.Tests.Infrastructure.Plugins;

/// <summary>
/// The manifest is where a plugin says which permission keys its routes will require.
/// </summary>
/// <remarks>
/// <para>
/// In the manifest rather than in code (<c>context.Export</c>) deliberately, even though
/// ADR-009 puts wiring in code: an operator has to be able to see what a plugin will ask for
/// <b>before</b> installing it, and a declaration that only exists once the plugin is running
/// is too late for that decision.
/// </para>
/// <para>
/// An undeclarable key makes the whole manifest invalid rather than being skipped. Skipping
/// would put the plugin back in the state this fixes — installed, serving 403, with the
/// reason two layers down.
/// </para>
/// </remarks>
public sealed class TheManifestCarriesDeclaredPermissionsTests
{
    [Fact]
    public async Task Declared_keys_reach_the_metadata()
    {
        var result = await ReadAsync("""
              "permissions": [
                { "key": "test.thing.update", "description": "Change a thing" },
                { "key": "test.thing.read" }
              ]
            """);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Collection(
            result.Registry!.DeclaredPermissions,
            first =>
            {
                Assert.Equal("test.thing.update", first.Key);
                Assert.Equal("Change a thing", first.Description);
            },
            second =>
            {
                Assert.Equal("test.thing.read", second.Key);
                Assert.Null(second.Description);
            });
    }

    [Fact]
    public async Task A_manifest_without_permissions_stays_valid()
    {
        var result = await ReadAsync(null);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Empty(result.Registry!.DeclaredPermissions);
    }

    [Fact]
    public async Task A_key_outside_the_plugins_namespace_invalidates_the_manifest()
    {
        // The one that matters: without this a plugin declares user.delete, an operator
        // grants what looks like the plugin's own permission, and hands it the host's.
        var result = await ReadAsync("""
              "permissions": [{ "key": "user.delete" }]
            """);

        Assert.False(result.IsValid);
        Assert.Contains("user.delete", result.ErrorMessage!, StringComparison.Ordinal);
        Assert.Equal(PluginRegistryErrorCodes.PermissionNotDeclarable, result.ErrorCode);
    }

    [Fact]
    public async Task A_key_without_a_known_action_invalidates_the_manifest()
    {
        var result = await ReadAsync("""
              "permissions": [{ "key": "test.thing.frobnicate" }]
            """);

        Assert.False(result.IsValid);
        Assert.Equal(PluginRegistryErrorCodes.PermissionNotDeclarable, result.ErrorCode);
    }

    [Fact]
    public async Task Duplicate_keys_are_collapsed_rather_than_refused()
    {
        // Repetition is untidy, not dangerous, and failing an install over it would be a
        // poor trade for the operator staring at the error.
        var result = await ReadAsync("""
              "permissions": [
                { "key": "test.thing.read" },
                { "key": "test.thing.read", "description": "again" }
              ]
            """);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Single(result.Registry!.DeclaredPermissions);
    }

    private static async Task<PluginPackageRegistryReadResult> ReadAsync(string? permissionsFragment)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"callora-permissions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var assemblyPath = Path.Combine(tempDir, "plugin.dll");
            await File.WriteAllBytesAsync(assemblyPath, []);

            var json = $$"""
                       {
                         "contractVersion": "v2",
                         "schemaVersion": "1.0",
                         "name": "Test Plugin",
                         "pluginId": "test",
                         "version": "1.0.0",
                         "assemblyFileName": "plugin.dll",
                         "entryTypeName": "Test.Plugin.Entry",
                         "capabilities": [],
                         "dependencies": {}{{(permissionsFragment is null ? "" : ",\n" + permissionsFragment)}}
                       }
                       """;
            await File.WriteAllTextAsync(Path.Combine(tempDir, "registry.json"), json, Encoding.UTF8);

            return await new JsonPluginPackageRegistryReader().ReadForAssemblyAsync(assemblyPath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
