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

        if (IsPluginInspectCommand(args))
        {
            var inspectParseResult = PluginInspectCommandParser.TryParse(args);
            if (!inspectParseResult.IsSuccess || inspectParseResult.Request is null)
            {
                await standardError.WriteLineAsync(inspectParseResult.ErrorMessage).ConfigureAwait(false);
                await standardError.WriteLineAsync(Usage).ConfigureAwait(false);
                return 1;
            }

            var inspector = new PluginInspector();
            var inspection = await inspector
                .InspectAsync(inspectParseResult.Request, cancellationToken)
                .ConfigureAwait(false);
            if (!inspection.IsSuccess)
            {
                await standardError.WriteLineAsync(inspection.ErrorMessage).ConfigureAwait(false);
                return 1;
            }

            await standardOutput.WriteAsync(inspection.Report).ConfigureAwait(false);
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
            // Warnungen erscheinen in beiden Fällen. Ein bestandener Lauf, der einen
            // Deprecation-Hinweis verschluckt, nimmt dem Plugin-Autor genau das Signal, an dem
            // er die Migration festmachen soll.
            foreach (var issue in testResult.Issues)
            {
                var target = issue.IsWarning ? standardOutput : standardError;
                var label = issue.IsWarning ? "warning" : "error";
                await target
                    .WriteLineAsync($"[{issue.Code}] {label}: {issue.Message} Fix: {issue.Remediation}")
                    .ConfigureAwait(false);
            }

            if (testResult.IsSuccess)
            {
                await standardOutput.WriteLineAsync("All contract checks passed.").ConfigureAwait(false);
                return 0;
            }

            return 1;
        }

        if (IsPluginSignCommand(args))
        {
            var signParseResult = PluginSignCommandParser.TryParse(args, currentDirectory);
            if (!signParseResult.IsSuccess || signParseResult.Request is null)
            {
                await standardError.WriteLineAsync(signParseResult.ErrorMessage).ConfigureAwait(false);
                await standardError.WriteLineAsync(Usage).ConfigureAwait(false);
                return 1;
            }

            var signer = new PluginSigner();
            var signResult = await signer.SignAsync(signParseResult.Request, cancellationToken).ConfigureAwait(false);
            if (!signResult.IsSuccess)
            {
                await standardError.WriteLineAsync(signResult.ErrorMessage).ConfigureAwait(false);
                return 1;
            }

            await standardOutput.WriteLineAsync($"Plugin signature written: {signResult.OutputPath}").ConfigureAwait(false);
            return 0;
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

    private static bool IsPluginInspectCommand(IReadOnlyList<string> args) =>
        args.Count >= 2
        && string.Equals(args[0], "plugin", StringComparison.Ordinal)
        && string.Equals(args[1], "inspect", StringComparison.Ordinal);

    private static bool IsPluginTestContractCommand(IReadOnlyList<string> args) =>
        args.Count >= 2
        && string.Equals(args[0], "plugin", StringComparison.OrdinalIgnoreCase)
        && string.Equals(args[1], "test-contract", StringComparison.OrdinalIgnoreCase);

    private static bool IsPluginSignCommand(IReadOnlyList<string> args) =>
        args.Count >= 2
        && string.Equals(args[0], "plugin", StringComparison.OrdinalIgnoreCase)
        && string.Equals(args[1], "sign", StringComparison.OrdinalIgnoreCase);

    private const string Usage =
        "Usage:\n"
        + "  callora plugin new [name] [--name <display-name>] [--id <plugin-id>] [--output <directory>] [--force]\n"
        + "  callora plugin test-contract --assembly <path-to-dll> [--registry <path-to-registry.json>] [--entry-type <full-type-name>]\n"
        + "  callora plugin inspect --assembly <path-to-dll> [--registry <path-to-registry.json>]\n"
        + "  callora plugin sign --plugin <plugin-directory> --key <private-key.pem> [--out <plugin.signature.json>]";
}
