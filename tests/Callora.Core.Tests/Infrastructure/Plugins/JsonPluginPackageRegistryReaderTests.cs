using System.Text;
using Callora.Core.Application.Plugins;
using Callora.Core.Infrastructure.Plugins;

namespace Callora.Core.Tests.Infrastructure.Plugins;

public sealed class JsonPluginPackageRegistryReaderTests
{
    [Fact]
    public async Task ReadForAssemblyAsync_UnknownContractVersion_ReturnsInvalidWithStableErrorCode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"callora-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var assemblyPath = Path.Combine(tempDir, "plugin.dll");
            await File.WriteAllBytesAsync(assemblyPath, []);

            var registryPath = Path.Combine(tempDir, "registry.json");
            var json = """
                       {
                         "contractVersion": "v99",
                         "schemaVersion": "1.0",
                         "name": "Test Plugin",
                         "pluginId": "test",
                         "version": "1.0.0",
                         "assemblyFileName": "plugin.dll",
                         "entryTypeName": "Test.Plugin.Entry",
                         "capabilities": [],
                         "dependencies": {}
                       }
                       """;
            await File.WriteAllTextAsync(registryPath, json, Encoding.UTF8);

            var sut = new JsonPluginPackageRegistryReader();
            var result = await sut.ReadForAssemblyAsync(assemblyPath);

            Assert.True(result.HasRegistryFile);
            Assert.False(result.IsValid);
            Assert.Null(result.Registry);
            Assert.Equal(PluginRegistryErrorCodes.ContractVersionUnsupported, result.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadForAssemblyAsync_SupportedContractVersion_ReturnsValidRegistry()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"callora-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var assemblyPath = Path.Combine(tempDir, "plugin.dll");
            await File.WriteAllBytesAsync(assemblyPath, []);

            var registryPath = Path.Combine(tempDir, "registry.json");
            var json = """
                       {
                         "contractVersion": "v2",
                         "schemaVersion": "1.0",
                         "name": "Test Plugin",
                         "pluginId": "test",
                         "version": "1.0.0",
                         "assemblyFileName": "plugin.dll",
                         "entryTypeName": "Test.Plugin.Entry",
                         "capabilities": [],
                         "dependencies": {}
                       }
                       """;
            await File.WriteAllTextAsync(registryPath, json, Encoding.UTF8);

            var sut = new JsonPluginPackageRegistryReader();
            var result = await sut.ReadForAssemblyAsync(assemblyPath);

            Assert.True(result.IsValid);
            Assert.NotNull(result.Registry);
            Assert.Equal("v2", result.Registry.ContractVersion);
            Assert.Null(result.WarningCode);
            Assert.Null(result.WarningMessage);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadForAssemblyAsync_DeprecatedContractVersion_ReturnsValidWithWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"callora-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var assemblyPath = Path.Combine(tempDir, "plugin.dll");
            await File.WriteAllBytesAsync(assemblyPath, []);

            var registryPath = Path.Combine(tempDir, "registry.json");
            var json = """
                       {
                         "contractVersion": "v1",
                         "schemaVersion": "1.0",
                         "name": "Test Plugin",
                         "pluginId": "test",
                         "version": "1.0.0",
                         "assemblyFileName": "plugin.dll",
                         "entryTypeName": "Test.Plugin.Entry",
                         "capabilities": [],
                         "dependencies": {}
                       }
                       """;
            await File.WriteAllTextAsync(registryPath, json, Encoding.UTF8);

            var sut = new JsonPluginPackageRegistryReader();
            var result = await sut.ReadForAssemblyAsync(assemblyPath);

            Assert.True(result.IsValid);
            Assert.NotNull(result.Registry);
            Assert.Equal("v1", result.Registry.ContractVersion);
            Assert.Equal(PluginRegistryErrorCodes.ContractVersionDeprecated, result.WarningCode);
            Assert.NotNull(result.WarningMessage);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadForAssemblyAsync_RemovedContractVersion_ReturnsInvalidWithStableErrorCode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"callora-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var assemblyPath = Path.Combine(tempDir, "plugin.dll");
            await File.WriteAllBytesAsync(assemblyPath, []);

            var registryPath = Path.Combine(tempDir, "registry.json");
            var json = """
                       {
                         "contractVersion": "v0",
                         "schemaVersion": "1.0",
                         "name": "Test Plugin",
                         "pluginId": "test",
                         "version": "1.0.0",
                         "assemblyFileName": "plugin.dll",
                         "entryTypeName": "Test.Plugin.Entry",
                         "capabilities": [],
                         "dependencies": {}
                       }
                       """;
            await File.WriteAllTextAsync(registryPath, json, Encoding.UTF8);

            var sut = new JsonPluginPackageRegistryReader();
            var result = await sut.ReadForAssemblyAsync(assemblyPath);

            Assert.True(result.HasRegistryFile);
            Assert.False(result.IsValid);
            Assert.Null(result.Registry);
            Assert.Equal(PluginRegistryErrorCodes.ContractVersionRemoved, result.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadForAssemblyAsync_AssemblyInBinDirectory_UsesRegistryFromParentDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"callora-registry-{Guid.NewGuid():N}");
        var binDir = Path.Combine(tempDir, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(binDir);
        try
        {
            var assemblyPath = Path.Combine(binDir, "plugin.dll");
            await File.WriteAllBytesAsync(assemblyPath, []);

            var registryPath = Path.Combine(tempDir, "registry.json");
            var json = """
                       {
                         "contractVersion": "v0",
                         "schemaVersion": "1.0",
                         "name": "Test Plugin",
                         "pluginId": "test",
                         "version": "1.0.0",
                         "assemblyFileName": "plugin.dll",
                         "entryTypeName": "Test.Plugin.Entry",
                         "capabilities": [],
                         "dependencies": {}
                       }
                       """;
            await File.WriteAllTextAsync(registryPath, json, Encoding.UTF8);

            var sut = new JsonPluginPackageRegistryReader();
            var result = await sut.ReadForAssemblyAsync(assemblyPath);

            Assert.True(result.HasRegistryFile);
            Assert.False(result.IsValid);
            Assert.Equal(PluginRegistryErrorCodes.ContractVersionRemoved, result.ErrorCode);
            Assert.Equal(registryPath, result.RegistryPath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
