namespace Callora.Host.Cli.Application;

internal sealed record PluginContractTestIssue(
    string Code,
    string Message,
    string Remediation);
