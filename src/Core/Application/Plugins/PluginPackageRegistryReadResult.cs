namespace Callora.Core.Application.Plugins;

public sealed record PluginPackageRegistryReadResult(
    bool HasRegistryFile,
    bool IsValid,
    string? RegistryPath,
    PluginPackageRegistryMetadata? Registry,
    string? ErrorMessage = null,
    string? ErrorCode = null,
    string? WarningMessage = null,
    string? WarningCode = null);
