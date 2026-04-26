namespace Callora.Host.Cli.Application;

/// <summary>
/// Input for contract checks against a plugin assembly and manifest.
/// </summary>
internal sealed record PluginContractTestRequest(
    string AssemblyPath,
    string? RegistryPath,
    string? EntryTypeName);
