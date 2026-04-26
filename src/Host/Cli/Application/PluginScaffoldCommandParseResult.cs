namespace Callora.Host.Cli.Application;

internal sealed record PluginScaffoldCommandParseResult(
    bool IsSuccess,
    PluginScaffoldRequest? Request,
    string ErrorMessage)
{
    public static PluginScaffoldCommandParseResult Success(PluginScaffoldRequest request) =>
        new(true, request, string.Empty);

    public static PluginScaffoldCommandParseResult Fail(string errorMessage) =>
        new(false, null, errorMessage);
}
