namespace Callora.Host.Backend.Application.Lifecycle;

public enum PluginLifecycleServiceStatus
{
    Ok = 0,
    BadRequest = 1,
    Forbidden = 2,
}

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
}

public static class PluginLifecycleWarningCodes
{
    public const string PluginContractVersionDeprecated = "PLUGIN_CONTRACT_VERSION_DEPRECATED";
}

public sealed record PluginLifecycleServiceResult(
    PluginLifecycleServiceStatus Status,
    bool IsSuccess,
    string? PluginId = null,
    string? Message = null,
    string? ErrorCode = null,
    string? WarningMessage = null,
    string? WarningCode = null);

public sealed record PluginInstallationSnapshot(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    int State,
    DateTimeOffset InstalledAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record InstallPluginCommand(
    string AssemblyPath,
    string? EntryTypeName = null,
    string? RequestedBy = null);

public sealed record InstallNuGetPluginCommand(
    string PackageId,
    string PackageVersion,
    string? AssemblyFileName = null,
    string? EntryTypeName = null,
    string? RequestedBy = null);

public sealed record UpdateNuGetPluginCommand(
    string PluginId,
    string PackageId,
    string PackageVersion,
    string? AssemblyFileName = null,
    string? EntryTypeName = null,
    string? RequestedBy = null);

public sealed record UpdateLocalPluginCommand(
    string PluginId,
    bool BuildIfNeeded = true,
    bool ForceBuild = false,
    string? RequestedBy = null);

public sealed record PluginLifecycleCommand(
    string PluginId,
    string? RequestedBy = null,
    string? WorkspaceKey = null);
