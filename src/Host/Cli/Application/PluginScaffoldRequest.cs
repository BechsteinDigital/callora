namespace Callora.Host.Cli.Application;

/// <summary>
/// Input data for creating a new plugin scaffold.
/// </summary>
public sealed record PluginScaffoldRequest(
    string Name,
    string PluginId,
    string OutputDirectory,
    bool Force);
