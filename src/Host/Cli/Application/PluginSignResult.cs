namespace Callora.Host.Cli.Application;

internal sealed class PluginSignResult
{
    private PluginSignResult(bool isSuccess, string? outputPath, string errorMessage)
    {
        IsSuccess = isSuccess;
        OutputPath = outputPath;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public string? OutputPath { get; }

    public string ErrorMessage { get; }

    public static PluginSignResult Success(string outputPath) =>
        new(true, outputPath, string.Empty);

    public static PluginSignResult Fail(string message) =>
        new(false, null, message);
}
