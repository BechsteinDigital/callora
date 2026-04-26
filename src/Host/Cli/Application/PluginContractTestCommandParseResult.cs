namespace Callora.Host.Cli.Application;

internal sealed record PluginContractTestCommandParseResult(
    bool IsSuccess,
    PluginContractTestRequest? Request,
    string ErrorMessage)
{
    public static PluginContractTestCommandParseResult Success(PluginContractTestRequest request) =>
        new(true, request, string.Empty);

    public static PluginContractTestCommandParseResult Fail(string errorMessage) =>
        new(false, null, errorMessage);
}
