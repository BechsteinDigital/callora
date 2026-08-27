using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Extensions;
using Callora.Core.Infrastructure.Plugins;
using System.Text;

namespace Callora.Core.Tests.Infrastructure.Plugins;

/// <summary>
/// The manifest declares extension registrations, and so does the runtime. Both name the
/// same two things — an extension-point id and a surface — and only one of them used to
/// check that they exist.
/// </summary>
/// <remarks>
/// <para>
/// <c>PluginExtensionSynchronizer</c> refuses an unknown point with
/// <c>PluginExtensionPointUnknown</c> and an unparseable surface with
/// <c>PluginExtensionSurfaceMismatch</c>. The manifest reader skipped both silently, so
/// <c>"workspace.navigation.mian"</c> installed, activated, reported healthy, and simply did
/// not appear.
/// </para>
/// <para>
/// Sharper than it first looks: CAL0004 exists specifically to stop a raw string literal
/// being passed as an extension-point id <b>in code</b>. The same value in the manifest got
/// no check at all — the rule guarded the door and left the window open.
/// </para>
/// </remarks>
public sealed class TheManifestChecksWhatTheRuntimeChecksTests
{
    [Fact]
    public async Task An_unknown_extension_point_invalidates_the_manifest()
    {
        var result = await ReadAsync("""
              "extensions": [{ "extensionPointId": "workspace.navigation.mian", "surface": "surface" }]
            """);

        Assert.False(result.IsValid);
        Assert.Contains("workspace.navigation.mian", result.ErrorMessage!, StringComparison.Ordinal);
        Assert.Equal(PluginRegistryErrorCodes.ExtensionPointUnknown, result.ErrorCode);
    }

    [Fact]
    public async Task An_unparseable_surface_invalidates_the_manifest()
    {
        var result = await ReadAsync($$"""
              "extensions": [{ "extensionPointId": "{{CalloraExtensionPoints.WorkspaceNavigationMain}}", "surface": "nowhere" }]
            """);

        Assert.False(result.IsValid);
        Assert.Equal(PluginRegistryErrorCodes.ExtensionSurfaceInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task A_known_registration_still_reads()
    {
        var result = await ReadAsync($$"""
              "extensions": [{ "extensionPointId": "{{CalloraExtensionPoints.AdminApiRoute}}", "surface": "admin" }]
            """);

        Assert.True(result.IsValid, result.ErrorMessage);
        var registration = Assert.Single(result.Registry!.ExtensionRegistrations!);
        Assert.Equal(CalloraExtensionPoints.AdminApiRoute, registration.ExtensionPointId);
    }

    [Fact]
    public async Task An_entry_naming_nothing_is_still_skipped()
    {
        // An empty array element is untidy, not a typo — nothing was misspelled, so there is
        // nothing to warn anyone about.
        var result = await ReadAsync("""
              "extensions": [{ }]
            """);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Empty(result.Registry!.ExtensionRegistrations!);
    }

    [Fact]
    public async Task A_manifest_without_extensions_stays_valid()
    {
        var result = await ReadAsync(null);

        Assert.True(result.IsValid, result.ErrorMessage);
    }

    private static async Task<PluginPackageRegistryReadResult> ReadAsync(string? extensionsFragment)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"callora-extensions-{Guid.NewGuid():N}");
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
                         "dependencies": {}{{(extensionsFragment is null ? "" : ",\n" + extensionsFragment)}}
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
