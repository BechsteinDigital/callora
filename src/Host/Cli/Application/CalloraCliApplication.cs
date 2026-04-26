namespace Callora.Host.Cli.Application;

/// <summary>
/// Entry facade for the Callora command-line interface.
/// </summary>
public static class CalloraCliApplication
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter standardOutput,
        TextWriter standardError,
        string currentDirectory,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args))
        {
            await standardOutput.WriteLineAsync(Usage).ConfigureAwait(false);
            return 0;
        }

        if (IsPluginNewCommand(args))
        {
            var scaffoldParseResult = PluginScaffoldCommandParser.TryParse(args, currentDirectory);
            if (!scaffoldParseResult.IsSuccess || scaffoldParseResult.Request is null)
            {
                await standardError.WriteLineAsync(scaffoldParseResult.ErrorMessage).ConfigureAwait(false);
                await standardError.WriteLineAsync(Usage).ConfigureAwait(false);
                return 1;
            }

            var scaffolder = new PluginScaffolder();
            var scaffoldResult = await scaffolder.ScaffoldAsync(
                    scaffoldParseResult.Request,
                    currentDirectory,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!scaffoldResult.IsSuccess)
            {
                await standardError.WriteLineAsync(scaffoldResult.ErrorMessage).ConfigureAwait(false);
                return 1;
            }

            await standardOutput.WriteLineAsync($"Plugin scaffold created: {scaffoldResult.OutputDirectory}")
                .ConfigureAwait(false);
            return 0;
        }

        if (IsPluginTestContractCommand(args))
        {
            var testParseResult = PluginContractTestCommandParser.TryParse(args);
            if (!testParseResult.IsSuccess || testParseResult.Request is null)
            {
                await standardError.WriteLineAsync(testParseResult.ErrorMessage).ConfigureAwait(false);
                await standardError.WriteLineAsync(Usage).ConfigureAwait(false);
                return 1;
            }

            var tester = new PluginContractTester();
            var testResult = await tester.TestAsync(testParseResult.Request, cancellationToken).ConfigureAwait(false);
            if (testResult.IsSuccess)
            {
                await standardOutput.WriteLineAsync("All contract checks passed.").ConfigureAwait(false);
                return 0;
            }

            foreach (var issue in testResult.Issues)
            {
                await standardError
                    .WriteLineAsync($"[{issue.Code}] {issue.Message} Fix: {issue.Remediation}")
                    .ConfigureAwait(false);
            }

            return 1;
        }

        await standardError.WriteLineAsync("Unsupported command.").ConfigureAwait(false);
        await standardError.WriteLineAsync(Usage).ConfigureAwait(false);
        return 1;
    }

    private static bool IsHelp(IReadOnlyList<string> args) =>
        args.Count == 1 && (args[0] is "--help" or "-h" or "help");

    private static bool IsPluginNewCommand(IReadOnlyList<string> args) =>
        args.Count >= 2
        && string.Equals(args[0], "plugin", StringComparison.OrdinalIgnoreCase)
        && string.Equals(args[1], "new", StringComparison.OrdinalIgnoreCase);

    private static bool IsPluginTestContractCommand(IReadOnlyList<string> args) =>
        args.Count >= 2
        && string.Equals(args[0], "plugin", StringComparison.OrdinalIgnoreCase)
        && string.Equals(args[1], "test-contract", StringComparison.OrdinalIgnoreCase);

    private const string Usage =
        "Usage:\n"
        + "  callora plugin new [name] [--name <display-name>] [--id <plugin-id>] [--output <directory>] [--force]\n"
        + "  callora plugin test-contract --assembly <path-to-dll> [--registry <path-to-registry.json>] [--entry-type <full-type-name>]";
}
