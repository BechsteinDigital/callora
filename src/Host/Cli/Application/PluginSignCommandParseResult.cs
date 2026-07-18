namespace Callora.Host.Cli.Application;

internal sealed class PluginSignCommandParseResult
{
    private PluginSignCommandParseResult(bool isSuccess, PluginSignRequest? request, string errorMessage)
    {
        IsSuccess = isSuccess;
        Request = request;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public PluginSignRequest? Request { get; }

    public string ErrorMessage { get; }

    public static PluginSignCommandParseResult Success(PluginSignRequest request) =>
        new(true, request, string.Empty);

    public static PluginSignCommandParseResult Fail(string message) =>
        new(false, null, message);
}
