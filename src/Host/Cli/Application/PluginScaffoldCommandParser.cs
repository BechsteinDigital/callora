namespace Callora.Host.Cli.Application;

internal static class PluginScaffoldCommandParser
{
    public static PluginScaffoldCommandParseResult TryParse(
        IReadOnlyList<string> args,
        string currentDirectory)
    {
        string? positionalName = null;
        string? name = null;
        string? pluginId = null;
        string? outputDirectory = null;
        var force = false;

        for (var index = 2; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--name":
                    if (!TryReadValue(args, ref index, out name))
                    {
                        return PluginScaffoldCommandParseResult.Fail("Missing value for --name.");
                    }

                    break;
                case "--id":
                    if (!TryReadValue(args, ref index, out pluginId))
                    {
                        return PluginScaffoldCommandParseResult.Fail("Missing value for --id.");
                    }

                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, out outputDirectory))
                    {
                        return PluginScaffoldCommandParseResult.Fail("Missing value for --output.");
                    }

                    break;
                case "--force":
                    force = true;
                    break;
                default:
                    if (argument.StartsWith("--", StringComparison.Ordinal))
                    {
                        return PluginScaffoldCommandParseResult.Fail($"Unknown option '{argument}'.");
                    }

                    if (positionalName is not null)
                    {
                        return PluginScaffoldCommandParseResult.Fail("Only one positional plugin name is allowed.");
                    }

                    positionalName = argument;
                    break;
            }
        }

        var effectiveName = name ?? positionalName;
        if (string.IsNullOrWhiteSpace(effectiveName))
        {
            return PluginScaffoldCommandParseResult.Fail("Plugin name is required.");
        }

        var safeSegment = PluginScaffoldNaming.ToSafePathSegment(effectiveName);
        var effectiveOutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(currentDirectory, "custom", "plugins", safeSegment)
            : outputDirectory;

        var effectivePluginId = string.IsNullOrWhiteSpace(pluginId)
            ? PluginScaffoldNaming.ToPluginId(effectiveName)
            : pluginId.Trim();

        return PluginScaffoldCommandParseResult.Success(
            new PluginScaffoldRequest(
                effectiveName.Trim(),
                effectivePluginId,
                effectiveOutputDirectory,
                force));
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
