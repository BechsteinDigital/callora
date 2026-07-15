namespace Callora.Core.Tests.Cli;

public sealed record BuildPluginUiAssetsScriptResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
