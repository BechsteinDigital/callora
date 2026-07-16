using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Cli;

/// <summary>
/// Dispatches console commands by name — the Callora counterpart of the Symfony
/// console <c>Application</c>. Commands come from DI (framework commands) plus the
/// live plugin catalog (plugin-exported commands), so the distribution's thin
/// <c>bin/console</c> equivalent only has to boot the host and call
/// <see cref="RunAsync"/>. No command logic lives in the skeleton.
/// </summary>
public sealed class CalloraConsoleRunner
{
    private readonly IReadOnlyList<ICalloraConsoleCommand> _commands;

    /// <summary>Creates a runner over the host-registered and plugin-exported commands.</summary>
    public CalloraConsoleRunner(IEnumerable<ICalloraConsoleCommand> hostCommands, ICalloraPluginCatalog pluginCatalog)
    {
        ArgumentNullException.ThrowIfNull(hostCommands);
        ArgumentNullException.ThrowIfNull(pluginCatalog);

        var byName = new Dictionary<string, ICalloraConsoleCommand>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in hostCommands.Concat(pluginCatalog.GetExports<ICalloraConsoleCommand>()))
        {
            // Host-registered commands win over a plugin exporting the same name.
            byName.TryAdd(command.Name, command);
        }

        _commands = byName.Values.OrderBy(command => command.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Runs the command named by the first argument; lists commands when absent or unknown.</summary>
    public async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        var name = args.Count > 0 ? args[0] : null;
        if (name is null or "help" or "--help" or "-h" or "list")
        {
            WriteCommandList(output);
            return 0;
        }

        var command = _commands.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        if (command is null)
        {
            output.WriteLine($"Unknown command '{name}'.");
            WriteCommandList(output);
            return 1;
        }

        return await command.ExecuteAsync(args.Skip(1).ToList(), output, cancellationToken).ConfigureAwait(false);
    }

    private void WriteCommandList(TextWriter output)
    {
        output.WriteLine("Available commands:");
        foreach (var command in _commands)
        {
            output.WriteLine($"  {command.Name,-28}{command.Description}");
        }
    }
}
