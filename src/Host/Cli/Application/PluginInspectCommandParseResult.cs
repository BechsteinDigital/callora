namespace Callora.Host.Cli.Application;

/// <summary>Outcome of parsing <c>plugin inspect</c> arguments.</summary>
internal sealed record PluginInspectCommandParseResult(
    bool IsSuccess,
    PluginInspectRequest? Request,
    string ErrorMessage)
{
    public static PluginInspectCommandParseResult Ok(PluginInspectRequest request) =>
        new(true, request, string.Empty);

    public static PluginInspectCommandParseResult Fail(string errorMessage) =>
        new(false, null, errorMessage);
}
