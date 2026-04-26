namespace Callora.Host.Cli.Application;

internal sealed record PluginScaffoldResult(
    bool IsSuccess,
    string OutputDirectory,
    string ErrorMessage)
{
    public static PluginScaffoldResult Success(string outputDirectory) =>
        new(true, outputDirectory, string.Empty);

    public static PluginScaffoldResult Fail(string errorMessage) =>
        new(false, string.Empty, errorMessage);
}
