namespace Callora.Host.Cli.Application;

internal sealed record PluginContractTestResult(
    bool IsSuccess,
    IReadOnlyList<PluginContractTestIssue> Issues)
{
    public static PluginContractTestResult Success() =>
        new(true, Array.Empty<PluginContractTestIssue>());

    public static PluginContractTestResult Failure(IReadOnlyList<PluginContractTestIssue> issues) =>
        new(false, issues);
}
