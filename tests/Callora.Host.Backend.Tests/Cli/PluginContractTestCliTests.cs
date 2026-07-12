using System.Diagnostics;
using Callora.Host.Cli.Application;

namespace Callora.Host.Backend.Tests.Cli;

// Baut pro Test ein komplettes dotnet-Projekt — lokal filterbar via
// --filter "Category!=Slow" für eine schnelle Feedback-Schleife.
[Trait("Category", "Slow")]
public sealed class PluginContractTestCliTests
{
    [Fact]
    public async Task PluginTestContract_ReferenceVoipPlugin_PassesMandatoryChecks()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var voipProjectPath = Path.Combine(repositoryRoot, "custom", "plugins", "Voip", "Callora.Plugins.Voip.csproj");
        var buildResult = await BuildProjectAsync(voipProjectPath, repositoryRoot).ConfigureAwait(false);
        Assert.True(buildResult.success, buildResult.output);

        var assemblyPath = Path.Combine(
            repositoryRoot,
            "custom",
            "plugins",
            "Voip",
            "bin",
            "Debug",
            "net10.0",
            "Callora.Plugins.Voip.dll");
        var registryPath = Path.Combine(repositoryRoot, "custom", "plugins", "Voip", "registry.json");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "test-contract", "--assembly", assemblyPath, "--registry", registryPath],
            stdout,
            stderr,
            repositoryRoot,
            CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.Contains("All contract checks passed", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PluginTestContract_WithValidPlugin_ReturnsSuccess()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var scaffoldDirectory = Path.Combine(
            Path.GetTempPath(),
            $"callora-contract-test-{Guid.NewGuid():N}",
            "AcmeVoice");

        using var scaffoldStdout = new StringWriter();
        using var scaffoldStderr = new StringWriter();
        var scaffoldExitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "new", "Acme Voice", "--output", scaffoldDirectory],
            scaffoldStdout,
            scaffoldStderr,
            repositoryRoot,
            CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(0, scaffoldExitCode);

        var csprojPath = Path.Combine(scaffoldDirectory, "Callora.Plugins.AcmeVoice.csproj");
        var buildResult = await BuildProjectAsync(csprojPath, repositoryRoot).ConfigureAwait(false);
        Assert.True(buildResult.success, buildResult.output);

        var assemblyPath = Path.Combine(scaffoldDirectory, "bin", "Debug", "net10.0", "Callora.Plugins.AcmeVoice.dll");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "test-contract", "--assembly", assemblyPath],
            stdout,
            stderr,
            repositoryRoot,
            CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(0, exitCode);
        Assert.Contains("All contract checks passed", stdout.ToString(), StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()));
    }

    [Fact]
    public async Task PluginTestContract_WithInvalidManifest_ReturnsActionableError()
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var scaffoldDirectory = Path.Combine(
            Path.GetTempPath(),
            $"callora-contract-invalid-{Guid.NewGuid():N}",
            "AcmeVoice");

        using var scaffoldStdout = new StringWriter();
        using var scaffoldStderr = new StringWriter();
        var scaffoldExitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "new", "Acme Voice", "--output", scaffoldDirectory],
            scaffoldStdout,
            scaffoldStderr,
            repositoryRoot,
            CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(0, scaffoldExitCode);

        var registryPath = Path.Combine(scaffoldDirectory, "registry.json");
        var invalidRegistry = """
{
  "contractVersion": "v1",
  "schemaVersion": "1.0",
  "name": "Broken Plugin",
  "version": "0.1.0",
  "assemblyFileName": "Callora.Plugins.AcmeVoice.dll",
  "entryTypeName": "Callora.Plugins.AcmeVoice.Application.AcmeVoicePlugin"
}
""";
        await File.WriteAllTextAsync(registryPath, invalidRegistry, CancellationToken.None).ConfigureAwait(false);

        var csprojPath = Path.Combine(scaffoldDirectory, "Callora.Plugins.AcmeVoice.csproj");
        var buildResult = await BuildProjectAsync(csprojPath, repositoryRoot).ConfigureAwait(false);
        Assert.True(buildResult.success, buildResult.output);

        var assemblyPath = Path.Combine(scaffoldDirectory, "bin", "Debug", "net10.0", "Callora.Plugins.AcmeVoice.dll");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "test-contract", "--assembly", assemblyPath, "--registry", registryPath],
            stdout,
            stderr,
            repositoryRoot,
            CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(1, exitCode);
        var errorOutput = stderr.ToString();
        Assert.Contains("MANIFEST_PLUGIN_ID_MISSING", errorOutput, StringComparison.Ordinal);
        Assert.Contains("registry.json field 'pluginId' is required", errorOutput, StringComparison.Ordinal);
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
