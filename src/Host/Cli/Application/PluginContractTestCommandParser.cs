namespace Callora.Host.Cli.Application;

internal static class PluginContractTestCommandParser
{
    public static PluginContractTestCommandParseResult TryParse(IReadOnlyList<string> args)
    {
        string? assemblyPath = null;
        string? registryPath = null;
        string? entryTypeName = null;

        for (var index = 2; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--assembly":
                    if (!TryReadValue(args, ref index, out assemblyPath))
                    {
                        return PluginContractTestCommandParseResult.Fail("Missing value for --assembly.");
                    }

                    break;
                case "--registry":
                    if (!TryReadValue(args, ref index, out registryPath))
                    {
                        return PluginContractTestCommandParseResult.Fail("Missing value for --registry.");
                    }

                    break;
                case "--entry-type":
                    if (!TryReadValue(args, ref index, out entryTypeName))
                    {
                        return PluginContractTestCommandParseResult.Fail("Missing value for --entry-type.");
                    }

                    break;
                default:
                    return PluginContractTestCommandParseResult.Fail($"Unknown option '{argument}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return PluginContractTestCommandParseResult.Fail("Option --assembly is required.");
        }

        return PluginContractTestCommandParseResult.Success(
            new PluginContractTestRequest(
                assemblyPath.Trim(),
                registryPath?.Trim(),
                entryTypeName?.Trim()));
    }

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string? value)
    {
        if (index + 1 >= args.Count)
        {
            value = null;
            return false;
        }

        value = args[index + 1];
        index++;
        return !string.IsNullOrWhiteSpace(value);
    }
}
