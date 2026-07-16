using Callora.Core.Extensibility;

namespace Callora.Core.Application.Cli;

/// <summary>
/// A console command runnable through the distribution's console entry point — the
/// Callora counterpart of a Symfony <c>console.command</c> service. Framework commands
/// live in Core and are registered in DI; plugins may export their own (extension
/// point). The thin skeleton runner boots the host and dispatches to these.
/// </summary>
[CalloraExtensible("Extension point — implement and export to contribute a console command (REV2 §8.2)")]
public interface ICalloraConsoleCommand
{
    /// <summary>Invocation name, e.g. <c>plugin:refresh</c>.</summary>
    string Name { get; }

    /// <summary>One-line description shown in the command list.</summary>
    string Description { get; }

    /// <summary>Runs the command with its arguments (the name already stripped).</summary>
    /// <returns>A process exit code (0 = success).</returns>
    Task<int> ExecuteAsync(IReadOnlyList<string> args, TextWriter output, CancellationToken cancellationToken = default);
}
