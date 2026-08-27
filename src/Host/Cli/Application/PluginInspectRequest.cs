namespace Callora.Host.Cli.Application;

/// <summary>What to inspect.</summary>
/// <param name="AssemblyPath">The plugin assembly.</param>
/// <param name="RegistryPath">Its manifest, when it is not beside the assembly.</param>
internal sealed record PluginInspectRequest(string AssemblyPath, string? RegistryPath);
