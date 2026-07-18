namespace Callora.Host.Cli.Application;

internal static class PluginSignCommandParser
{
    public static PluginSignCommandParseResult TryParse(IReadOnlyList<string> args, string currentDirectory)
    {
        string? pluginDirectory = null;
        string? keyPath = null;
        string? outputPath = null;

        for (var index = 2; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--plugin":
                    if (!TryReadValue(args, ref index, out pluginDirectory))
                    {
                        return PluginSignCommandParseResult.Fail("Missing value for --plugin.");
                    }

                    break;
                case "--key":
                    if (!TryReadValue(args, ref index, out keyPath))
                    {
                        return PluginSignCommandParseResult.Fail("Missing value for --key.");
                    }

                    break;
                case "--out":
                    if (!TryReadValue(args, ref index, out outputPath))
                    {
                        return PluginSignCommandParseResult.Fail("Missing value for --out.");
                    }

                    break;
                default:
                    return PluginSignCommandParseResult.Fail($"Unknown option '{argument}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return PluginSignCommandParseResult.Fail("Option --plugin is required.");
        }

        if (string.IsNullOrWhiteSpace(keyPath))
        {
            return PluginSignCommandParseResult.Fail("Option --key is required.");
        }

        return PluginSignCommandParseResult.Success(
            new PluginSignRequest(
                Resolve(currentDirectory, pluginDirectory.Trim()),
                Resolve(currentDirectory, keyPath.Trim()),
                string.IsNullOrWhiteSpace(outputPath) ? null : Resolve(currentDirectory, outputPath.Trim())));
    }

    private static string Resolve(string currentDirectory, string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(currentDirectory, path));

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string? value)
    {
        if (index + 1 >= args.Count)
        {
            value = null;
            return false;
        }

        value = args[++index];
        return true;
    }
}
