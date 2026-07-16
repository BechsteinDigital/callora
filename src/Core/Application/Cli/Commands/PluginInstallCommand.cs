using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Cli.Commands;

/// <summary>
/// <c>plugin:install &lt;pluginId&gt;</c> — installs a discovered local plugin by id,
/// resolving (and building from its csproj if needed) its assembly first.
/// </summary>
internal sealed class PluginInstallCommand(
    ILocalPluginInstallSourceResolver installSourceResolver,
    IPluginLifecycleService lifecycleService) : ICalloraConsoleCommand
{
    public string Name => "plugin:install";

    public string Description => "Install a local plugin by id (builds from source if needed).";

    public async Task<int> ExecuteAsync(IReadOnlyList<string> args, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        if (args.Count == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            output.WriteLine("Usage: plugin:install <pluginId>");
            return 1;
        }

        var pluginId = args[0];
        var source = await installSourceResolver
            .ResolveForInstallAsync(pluginId, buildIfNeeded: true, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!source.IsSuccess || string.IsNullOrWhiteSpace(source.AssemblyPath))
        {
            output.WriteLine($"plugin:install: {pluginId} FAILED — {source.Message ?? source.ErrorCode ?? "could not resolve assembly"}");
            return 1;
        }

        var result = await lifecycleService
            .InstallAsync(new InstallPluginCommand(source.AssemblyPath, source.EntryTypeName, "cli:plugin-install"), cancellationToken)
            .ConfigureAwait(false);
        output.WriteLine(result.IsSuccess
            ? $"plugin:install: {pluginId} — {result.Message ?? "installed"}"
            : $"plugin:install: {pluginId} FAILED — {result.Message ?? result.ErrorCode ?? "error"}");
        return result.IsSuccess ? 0 : 1;
    }
}
