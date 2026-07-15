using Callora.Core.Application.Lifecycle;
using Callora.Core.Infrastructure.Plugins;
using Callora.Core.Tests.Support;
using Callora.Hosting.Application.Options;

namespace Callora.Core.Tests.Infrastructure.Plugins;

public sealed class LocalPluginInstallSourceResolverTests
{
    [Fact]
    public async Task ResolveForInstallAsync_PrecompiledDllExists_DoesNotBuild()
    {
        var tempDir = CreateTempDirectoryPath();
        try
        {
            var pluginRoot = CreatePluginLayout(
                tempDir,
                pluginId: "template-alpha",
                assemblyFileName: "Callora.Plugins.TemplateAlpha.dll",
                withProject: false);
            File.WriteAllText(Path.Combine(pluginRoot, "Callora.Plugins.TemplateAlpha.dll"), "stub");

            var builder = new ScriptedLocalPluginProjectBuilder();
            var sut = new LocalPluginInstallSourceResolver(
                new CalloraHostingOptions { PluginDirectory = tempDir },
                builder);

            var result = await sut.ResolveForInstallAsync("template-alpha", buildIfNeeded: true);

            Assert.True(result.IsSuccess);
            Assert.False(result.UsedBuild);
            Assert.NotNull(result.AssemblyPath);
            Assert.Empty(builder.BuildCalls);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveForInstallAsync_NoDllAndBuildDisabled_ReturnsBuildRequired()
    {
        var tempDir = CreateTempDirectoryPath();
        try
        {
            CreatePluginLayout(
                tempDir,
                pluginId: "template-beta",
                assemblyFileName: "Callora.Plugins.TemplateBeta.dll",
                withProject: true);

            var builder = new ScriptedLocalPluginProjectBuilder();
            var sut = new LocalPluginInstallSourceResolver(
                new CalloraHostingOptions { PluginDirectory = tempDir },
                builder);

            var result = await sut.ResolveForInstallAsync("template-beta", buildIfNeeded: false);

            Assert.False(result.IsSuccess);
            Assert.Equal(PluginLifecycleErrorCodes.LocalPluginBuildRequired, result.ErrorCode);
            Assert.Empty(builder.BuildCalls);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveForInstallAsync_NoDllAndBuildEnabled_BuildsAndReturnsAssembly()
    {
        var tempDir = CreateTempDirectoryPath();
        try
        {
            var pluginRoot = CreatePluginLayout(
                tempDir,
                pluginId: "template-gamma",
                assemblyFileName: "Callora.Plugins.TemplateGamma.dll",
                withProject: true);
            var expectedAssemblyPath = Path.Combine(pluginRoot, "bin", "Debug", "net10.0", "Callora.Plugins.TemplateGamma.dll");

            var builder = new ScriptedLocalPluginProjectBuilder
            {
                AfterBuild = _ =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(expectedAssemblyPath)!);
                    File.WriteAllText(expectedAssemblyPath, "stub");
                }
            };
            var sut = new LocalPluginInstallSourceResolver(
                new CalloraHostingOptions { PluginDirectory = tempDir },
                builder);

            var result = await sut.ResolveForInstallAsync("template-gamma", buildIfNeeded: true);

            Assert.True(result.IsSuccess);
            Assert.True(result.UsedBuild);
            Assert.Equal(Path.GetFullPath(expectedAssemblyPath), result.AssemblyPath);
            Assert.Single(builder.BuildCalls);
            Assert.Contains("|force=False", builder.BuildCalls[0], StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveForInstallAsync_ForceBuild_WithExistingDll_BuildsAndUsesRebuiltAssembly()
    {
        var tempDir = CreateTempDirectoryPath();
        try
        {
            var pluginRoot = CreatePluginLayout(
                tempDir,
                pluginId: "template-delta",
                assemblyFileName: "Callora.Plugins.TemplateDelta.dll",
                withProject: true);

            var directDllPath = Path.Combine(pluginRoot, "Callora.Plugins.TemplateDelta.dll");
            File.WriteAllText(directDllPath, "old");

            var rebuiltPath = Path.Combine(pluginRoot, "bin", "Debug", "net10.0", "Callora.Plugins.TemplateDelta.dll");
            var builder = new ScriptedLocalPluginProjectBuilder
            {
                AfterBuild = _ =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(rebuiltPath)!);
                    File.WriteAllText(rebuiltPath, "rebuilt");
                }
            };

            var sut = new LocalPluginInstallSourceResolver(
                new CalloraHostingOptions { PluginDirectory = tempDir },
                builder);

            var result = await sut.ResolveForInstallAsync("template-delta", buildIfNeeded: true, forceBuild: true);

            Assert.True(result.IsSuccess);
            Assert.True(result.UsedBuild);
            Assert.Single(builder.BuildCalls);
            Assert.Contains("|force=True", builder.BuildCalls[0], StringComparison.Ordinal);
            Assert.Equal(Path.GetFullPath(rebuiltPath), result.AssemblyPath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirectoryPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "callora-local-plugin-install-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreatePluginLayout(
        string pluginDirectory,
        string pluginId,
        string assemblyFileName,
        bool withProject)
    {
        var pluginRoot = Path.Combine(pluginDirectory, pluginId);
        Directory.CreateDirectory(pluginRoot);

        var registry = $$"""
            {
              "contractVersion": "v1",
              "schemaVersion": "1.0",
              "name": "{{pluginId}}",
              "pluginId": "{{pluginId}}",
              "version": "1.0.0",
              "assemblyFileName": "{{assemblyFileName}}",
              "entryTypeName": "Callora.Plugins.{{pluginId}}.Entry"
            }
            """;
        File.WriteAllText(Path.Combine(pluginRoot, "registry.json"), registry);

        if (withProject)
        {
            File.WriteAllText(Path.Combine(pluginRoot, $"{pluginId}.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        }

        return pluginRoot;
    }
}
