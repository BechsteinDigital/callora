namespace Callora.Host.Cli.Application;

internal static class PluginInspectCommandParser
{
    public static PluginInspectCommandParseResult TryParse(IReadOnlyList<string> args)
    {
        string? assemblyPath = null;
        string? registryPath = null;

        for (var index = 2; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--assembly":
                    if (!TryReadValue(args, ref index, out assemblyPath))
                    {
                        return PluginInspectCommandParseResult.Fail("Missing value for --assembly.");
                    }

                    break;
                case "--registry":
                    if (!TryReadValue(args, ref index, out registryPath))
                    {
                        return PluginInspectCommandParseResult.Fail("Missing value for --registry.");
                    }

                    break;
                default:
                    // Refused rather than ignored: a mistyped "--registy" would otherwise
                    // silently inspect the manifest beside the assembly and report a plugin
                    // that does not exist in that shape.
                    return PluginInspectCommandParseResult.Fail($"Unknown option '{argument}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return PluginInspectCommandParseResult.Fail("Missing required option --assembly.");
        }

        return PluginInspectCommandParseResult.Ok(new PluginInspectRequest(assemblyPath, registryPath));
    }

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string? value)
    {
        if (index + 1 >= args.Count)
        {
            value = null;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }
}
