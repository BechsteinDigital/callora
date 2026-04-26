using System.Diagnostics;
using Callora.Host.Backend.Application.Abstractions.Plugins;

namespace Callora.Host.Backend.Infrastructure.Plugins;

public sealed class LocalPluginProjectBuilder : ILocalPluginProjectBuilder
{
    public async Task<LocalPluginProjectBuildResult> BuildAsync(
        string projectPath,
        bool forceRebuild = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return new LocalPluginProjectBuildResult(false, "Project path is empty.");
        }

        var fullProjectPath = Path.GetFullPath(projectPath);
        if (!File.Exists(fullProjectPath))
        {
            return new LocalPluginProjectBuildResult(false, $"Project file '{fullProjectPath}' does not exist.");
        }

        var projectDirectory = Path.GetDirectoryName(fullProjectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return new LocalPluginProjectBuildResult(false, $"Could not resolve project directory for '{fullProjectPath}'.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = projectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(fullProjectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Debug");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("minimal");
        if (forceRebuild)
        {
            startInfo.ArgumentList.Add("--no-incremental");
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        var combined = string.Join(
            Environment.NewLine,
            new[] { output, error }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

        if (process.ExitCode == 0)
        {
            return new LocalPluginProjectBuildResult(true, "Plugin project compiled successfully.");
        }

        var message = string.IsNullOrWhiteSpace(combined)
            ? $"dotnet build failed with exit code {process.ExitCode}."
            : combined;
        return new LocalPluginProjectBuildResult(false, message);
    }
}
