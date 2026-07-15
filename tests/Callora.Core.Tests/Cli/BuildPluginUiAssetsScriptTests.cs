using System.Diagnostics;
using System.Text.Json;

namespace Callora.Core.Tests.Cli;

// Führt das Build-Script als externen Prozess aus — lokal filterbar via
// --filter "Category!=Slow" für eine schnelle Feedback-Schleife.
[Trait("Category", "Slow")]
public sealed class BuildPluginUiAssetsScriptTests
{
    [Fact]
    public async Task Script_GeneratesDeterministicManifest()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-ui-assets-{Guid.NewGuid():N}");
        var pluginsRoot = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsRoot);

        try
        {
            CreatePlugin(
                pluginsRoot,
                "PluginZ",
                createAdmin: true,
                createWorkspace: true,
                createTemplate: true);
            CreatePlugin(
                pluginsRoot,
                "PluginA",
                createAdmin: true,
                createWorkspace: false,
                createTemplate: true);

            var outPath1 = Path.Combine(tempRoot, "manifest-1.json");
            var outPath2 = Path.Combine(tempRoot, "manifest-2.json");

            await RunScriptAsync(pluginsRoot, outPath1);
            await RunScriptAsync(pluginsRoot, outPath2);

            using var doc1 = JsonDocument.Parse(await File.ReadAllTextAsync(outPath1));
            using var doc2 = JsonDocument.Parse(await File.ReadAllTextAsync(outPath2));

            var entries1 = doc1.RootElement.GetProperty("entries").EnumerateArray().Select(x => x.GetRawText()).ToArray();
            var entries2 = doc2.RootElement.GetProperty("entries").EnumerateArray().Select(x => x.GetRawText()).ToArray();

            Assert.Equal(entries1, entries2);
            Assert.Equal("PluginA", doc1.RootElement.GetProperty("entries")[0].GetProperty("pluginId").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Script_FailsWhenSurfaceDirectoryHasNoEntryFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-ui-assets-{Guid.NewGuid():N}");
        var pluginsRoot = Path.Combine(tempRoot, "plugins");
        Directory.CreateDirectory(pluginsRoot);

        try
        {
            var pluginDir = Path.Combine(pluginsRoot, "BrokenPlugin", "src", "Resources", "public", "admin");
            Directory.CreateDirectory(pluginDir);
            await File.WriteAllTextAsync(Path.Combine(pluginDir, "README.md"), "missing entry");

            var result = await RunScriptAsync(pluginsRoot, Path.Combine(tempRoot, "manifest.json"), expectSuccess: false);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Missing admin entry file", result.StandardError + result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void CreatePlugin(
        string pluginsRoot,
        string pluginName,
        bool createAdmin,
        bool createWorkspace,
        bool createTemplate)
    {
        if (createAdmin)
        {
            var adminDir = Path.Combine(pluginsRoot, pluginName, "src", "Resources", "app", "admin", "src");
            Directory.CreateDirectory(adminDir);
            File.WriteAllText(Path.Combine(adminDir, "main.js"), "export const admin = true;");
        }

        if (createWorkspace)
        {
            var workspaceDir = Path.Combine(pluginsRoot, pluginName, "src", "Resources", "app", "workspace", "src");
            Directory.CreateDirectory(workspaceDir);
            File.WriteAllText(Path.Combine(workspaceDir, "main.js"), "export const workspace = true;");
        }

        if (createTemplate)
        {
            var templateDir = Path.Combine(pluginsRoot, pluginName, "src", "Resources", "views", "workspace", "layouts");
            Directory.CreateDirectory(templateDir);
            File.WriteAllText(Path.Combine(templateDir, "dashboard.html"), "<div>template</div>");
        }
    }

    private static async Task<BuildPluginUiAssetsScriptResult> RunScriptAsync(
        string pluginsRoot,
        string outPath,
        bool expectSuccess = true)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"scripts/build-plugin-ui-assets.sh --plugins-root \"{pluginsRoot}\" --out \"{outPath}\"",
                WorkingDirectory = ResolveRepositoryRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }
        };

        process.Start();
        var stdOut = await process.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (expectSuccess)
        {
            Assert.Equal(0, process.ExitCode);
            Assert.True(File.Exists(outPath));
        }

        return new BuildPluginUiAssetsScriptResult(process.ExitCode, stdOut, stdErr);
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var scriptPath = Path.Combine(current.FullName, "scripts", "build-plugin-ui-assets.sh");
            if (File.Exists(scriptPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found for script execution.");
    }
}
