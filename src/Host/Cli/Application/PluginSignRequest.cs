namespace Callora.Host.Cli.Application;

/// <summary>Input for signing a plugin directory into a plugin.signature.json.</summary>
internal sealed record PluginSignRequest(string PluginDirectory, string KeyPath, string? OutputPath);
