namespace Callora.Core.Application.Lifecycle;

public static class PluginLifecycleErrorCodes
{
    public const string LocalPluginIdMissing = "LOCAL_PLUGIN_ID_MISSING";
    public const string LocalPluginDirectoryMissing = "LOCAL_PLUGIN_DIRECTORY_MISSING";
    public const string LocalPluginNotFound = "LOCAL_PLUGIN_NOT_FOUND";
    public const string LocalPluginRegistryInvalid = "LOCAL_PLUGIN_REGISTRY_INVALID";
    public const string LocalPluginBuildRequired = "LOCAL_PLUGIN_BUILD_REQUIRED";
    public const string LocalPluginProjectMissing = "LOCAL_PLUGIN_PROJECT_MISSING";
    public const string LocalPluginBuildFailed = "LOCAL_PLUGIN_BUILD_FAILED";
    public const string LocalPluginAssemblyMissingAfterBuild = "LOCAL_PLUGIN_ASSEMBLY_MISSING_AFTER_BUILD";
    public const string PluginContractVersionUnsupported = "PLUGIN_CONTRACT_VERSION_UNSUPPORTED";
    public const string PluginContractVersionRemoved = "PLUGIN_CONTRACT_VERSION_REMOVED";
    public const string PluginRegistryInvalid = "PLUGIN_REGISTRY_INVALID";
    public const string PluginAssemblyFileNameMismatch = "PLUGIN_ASSEMBLY_FILENAME_MISMATCH";
    public const string PluginRegistryPluginIdMismatch = "PLUGIN_REGISTRY_PLUGIN_ID_MISMATCH";
    public const string PluginExtensionPointUnknown = "PLUGIN_EXTENSION_POINT_UNKNOWN";
    public const string PluginExtensionSurfaceMismatch = "PLUGIN_EXTENSION_SURFACE_MISMATCH";
    public const string PluginExtensionScopeMissing = "PLUGIN_EXTENSION_SCOPE_MISSING";
    public const string PluginPackageUnsigned = "PLUGIN_PACKAGE_UNSIGNED";
    public const string PluginPackageSignatureInvalid = "PLUGIN_PACKAGE_SIGNATURE_INVALID";
    public const string PluginPackageSignerUntrusted = "PLUGIN_PACKAGE_SIGNER_UNTRUSTED";
    public const string PluginUpdateTargetNotFound = "PLUGIN_UPDATE_TARGET_NOT_FOUND";
    public const string PluginRollbackFailed = "PLUGIN_ROLLBACK_FAILED";
    public const string PluginRequiredCapabilityMissing = "PLUGIN_REQUIRED_CAPABILITY_MISSING";
    public const string PluginCapabilityInUse = "PLUGIN_CAPABILITY_IN_USE";
}
