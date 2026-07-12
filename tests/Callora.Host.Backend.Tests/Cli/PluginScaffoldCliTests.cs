using System.Diagnostics;
using Callora.Host.Cli.Application;

namespace Callora.Host.Backend.Tests.Cli;

// Baut pro Test ein komplettes dotnet-Projekt — lokal filterbar via
// --filter "Category!=Slow" für eine schnelle Feedback-Schleife.
[Trait("Category", "Slow")]
public sealed class PluginScaffoldCliTests
{
    [Fact]
    public async Task PluginNew_GeneratesBuildableScaffoldWithManifestAndExtension()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"callora-plugin-scaffold-{Guid.NewGuid():N}",
            "AcmeDialer");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "new", "Acme Dialer", "--output", outputDirectory],
            stdout,
            stderr,
            repositoryRoot,
            CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.True(Directory.Exists(outputDirectory));

        var csprojPath = Path.Combine(outputDirectory, "Callora.Plugins.AcmeDialer.csproj");
        var pluginClassPath = Path.Combine(outputDirectory, "Application", "AcmeDialerPlugin.cs");
        var registryPath = Path.Combine(outputDirectory, "registry.json");

        Assert.True(File.Exists(csprojPath));
        Assert.True(File.Exists(pluginClassPath));
        Assert.True(File.Exists(registryPath));

        var registry = await File.ReadAllTextAsync(registryPath).ConfigureAwait(false);
        Assert.Contains("\"pluginId\": \"acme-dialer\"", registry, StringComparison.Ordinal);
        Assert.Contains("\"extensions\"", registry, StringComparison.Ordinal);
        Assert.Contains("\"workspace.navigation.main\"", registry, StringComparison.Ordinal);

        var buildResult = await BuildProjectAsync(csprojPath, repositoryRoot).ConfigureAwait(false);
        Assert.True(buildResult.success, buildResult.output);
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "Callora.Host.sln");
            if (File.Exists(solutionPath))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not resolve repository root from test base directory.");
    }

    private static async Task<(bool success, string output)> BuildProjectAsync(string projectPath, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectPath}\" --nologo --verbosity minimal",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        var combined = string.Concat(output, Environment.NewLine, error);

        return (process.ExitCode == 0, combined);
    }
}
