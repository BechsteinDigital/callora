namespace Callora.Host.Backend.Application.Abstractions.Plugins;

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
