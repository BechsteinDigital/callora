using Callora.Core.Application.Lifecycle;

namespace Callora.Core.Application.Cli.Commands;

/// <summary>
/// Base for single-plugin lifecycle console commands: parses the plugin id, runs the
/// operation, and prints a uniform success/failure line with the right exit code
/// (Shopware AbstractPluginLifecycleCommand equivalent).
/// </summary>
internal abstract class PluginConsoleCommandBase(IPluginLifecycleService lifecycleService) : ICalloraConsoleCommand
{
    protected IPluginLifecycleService LifecycleService { get; } = lifecycleService;

    public abstract string Name { get; }

    public abstract string Description { get; }

    public async Task<int> ExecuteAsync(IReadOnlyList<string> args, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        if (args.Count == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            output.WriteLine($"Usage: {Name} <pluginId>");
            return 1;
        }

        var pluginId = args[0];
        var result = await RunAsync(pluginId, cancellationToken).ConfigureAwait(false);
        output.WriteLine(result.IsSuccess
            ? $"{Name}: {pluginId} — {result.Message ?? "ok"}"
            : $"{Name}: {pluginId} FAILED — {result.Message ?? result.ErrorCode ?? "error"}");
        return result.IsSuccess ? 0 : 1;
    }

    protected abstract Task<PluginLifecycleServiceResult> RunAsync(string pluginId, CancellationToken cancellationToken);
}
