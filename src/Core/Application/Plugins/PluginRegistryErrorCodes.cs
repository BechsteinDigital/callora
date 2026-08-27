namespace Callora.Core.Application.Plugins;

public static class PluginRegistryErrorCodes
{
    public const string ContractVersionMissing = "PLUGIN_CONTRACT_VERSION_MISSING";
    public const string ContractVersionUnsupported = "PLUGIN_CONTRACT_VERSION_UNSUPPORTED";
    public const string ContractVersionRemoved = "PLUGIN_CONTRACT_VERSION_REMOVED";
    public const string ContractVersionDeprecated = "PLUGIN_CONTRACT_VERSION_DEPRECATED";

    /// <summary>
    /// Das Manifest deklariert einen Schlüssel, den dieses Plugin nicht deklarieren darf —
    /// außerhalb seines Namensraums oder ohne bekannte Aktion.
    /// </summary>
    public const string PermissionNotDeclarable = "PLUGIN_PERMISSION_NOT_DECLARABLE";

    /// <summary>
    /// Das Manifest nennt einen Erweiterungspunkt, den es nicht gibt. Derselbe Befund, den der
    /// Laufzeitpfad als <c>PluginExtensionPointUnknown</c> meldet — hier nur früher.
    /// </summary>
    public const string ExtensionPointUnknown = "PLUGIN_EXTENSION_POINT_UNKNOWN";

    public const string ExtensionPointIdMissing = "PLUGIN_EXTENSION_POINT_ID_MISSING";
    public const string ExtensionSurfaceMissing = "PLUGIN_EXTENSION_SURFACE_MISSING";
    public const string ExtensionSurfaceInvalid = "PLUGIN_EXTENSION_SURFACE_INVALID";
}
