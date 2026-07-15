using System.Diagnostics;
using Callora.Host.Cli.Application;

namespace Callora.Core.Tests.Cli;

/// <summary>
/// Scaffolds and builds the "Acme Voice" example plugin exactly once per test
/// run. CLI tests share the built artifact instead of scaffolding and building
/// their own project per fact (PLAT-221).
/// </summary>
public sealed class ScaffoldedPluginFixture : IAsyncLifetime
{
    private string _tempRoot = string.Empty;

    public string RepositoryRoot { get; private set; } = string.Empty;

    public string ScaffoldDirectory { get; private set; } = string.Empty;

    public string AssemblyPath { get; private set; } = string.Empty;

    public string RegistryPath { get; private set; } = string.Empty;

    public string CsprojPath { get; private set; } = string.Empty;

    public string BuildOutput { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        RepositoryRoot = ResolveRepositoryRoot();
        _tempRoot = Path.Combine(Path.GetTempPath(), $"callora-scaffold-fixture-{Guid.NewGuid():N}");
        ScaffoldDirectory = Path.Combine(_tempRoot, "AcmeVoice");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await CalloraCliApplication.RunAsync(
            ["plugin", "new", "Acme Voice", "--output", ScaffoldDirectory],
            stdout,
            stderr,
            RepositoryRoot,
            CancellationToken.None);

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Plugin scaffold failed with exit code {exitCode}: {stderr}{stdout}");
        }

        CsprojPath = Path.Combine(ScaffoldDirectory, "Callora.Plugins.AcmeVoice.csproj");
        RegistryPath = Path.Combine(ScaffoldDirectory, "registry.json");
        AssemblyPath = Path.Combine(ScaffoldDirectory, "bin", "Debug", "net10.0", "Callora.Plugins.AcmeVoice.dll");

        var (success, output) = await BuildProjectAsync(CsprojPath, RepositoryRoot);
        BuildOutput = output;
        if (!success)
        {
            throw new InvalidOperationException($"Scaffolded plugin build failed: {output}");
        }
    }

    public Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_tempRoot) && Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    public static string ResolveRepositoryRoot()
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

    public static async Task<(bool Success, string Output)> BuildProjectAsync(string projectPath, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                // Ohne -nodeReuse:false/UseSharedCompilation=false hinterlässt der
                // Build MSBuild-/Roslyn-Worker, die die geerbten Pipe-Handles offen
                // halten — ReadToEndAsync hängt dann bis zu deren Idle-Timeout
                // (~15 min pro Testlauf, PLAT-221).
                Arguments = $"build \"{projectPath}\" --nologo --verbosity minimal -nodeReuse:false -p:UseSharedCompilation=false",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;
        var combined = string.Concat(output, Environment.NewLine, error);

        return (process.ExitCode == 0, combined);
    }
}
