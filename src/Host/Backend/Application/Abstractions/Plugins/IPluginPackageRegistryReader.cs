using Callora.Host.Backend.Domain.Extensions;

namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public interface IPluginPackageRegistryReader
{
    ValueTask<PluginPackageRegistryReadResult> ReadForAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}

public static class PluginRegistryErrorCodes
{
    public const string ContractVersionMissing = "PLUGIN_CONTRACT_VERSION_MISSING";
    public const string ContractVersionUnsupported = "PLUGIN_CONTRACT_VERSION_UNSUPPORTED";
    public const string ContractVersionRemoved = "PLUGIN_CONTRACT_VERSION_REMOVED";
    public const string ContractVersionDeprecated = "PLUGIN_CONTRACT_VERSION_DEPRECATED";
    public const string ExtensionPointIdMissing = "PLUGIN_EXTENSION_POINT_ID_MISSING";
    public const string ExtensionSurfaceMissing = "PLUGIN_EXTENSION_SURFACE_MISSING";
    public const string ExtensionSurfaceInvalid = "PLUGIN_EXTENSION_SURFACE_INVALID";
}

public sealed record PluginPackageRegistryReadResult(
    bool HasRegistryFile,
    bool IsValid,
    string? RegistryPath,
    PluginPackageRegistryMetadata? Registry,
    string? ErrorMessage = null,
    string? ErrorCode = null,
    string? WarningMessage = null,
    string? WarningCode = null);

public sealed record PluginPackageRegistryMetadata(
    string ContractVersion,
    string SchemaVersion,
    string Name,
    string PluginId,
    string Version,
    string AssemblyFileName,
    string EntryTypeName,
    IReadOnlyList<string> Capabilities,
    IReadOnlyDictionary<string, string> Dependencies,
    IReadOnlyList<PluginPackageExtensionRegistration>? ExtensionRegistrations = null);
