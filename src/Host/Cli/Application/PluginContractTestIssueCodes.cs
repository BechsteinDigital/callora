namespace Callora.Host.Cli.Application;

internal static class PluginContractTestIssueCodes
{
    public const string AssemblyNotFound = "ASSEMBLY_NOT_FOUND";
    public const string ManifestNotFound = "MANIFEST_NOT_FOUND";
    public const string ManifestParseError = "MANIFEST_PARSE_ERROR";
    public const string ManifestContractVersionMissing = "MANIFEST_CONTRACT_VERSION_MISSING";
    public const string ManifestContractVersionUnsupported = "MANIFEST_CONTRACT_VERSION_UNSUPPORTED";
    public const string ManifestSchemaVersionMissing = "MANIFEST_SCHEMA_VERSION_MISSING";
    public const string ManifestNameMissing = "MANIFEST_NAME_MISSING";
    public const string ManifestPluginIdMissing = "MANIFEST_PLUGIN_ID_MISSING";
    public const string ManifestVersionMissing = "MANIFEST_VERSION_MISSING";
    public const string ManifestAssemblyFileNameMissing = "MANIFEST_ASSEMBLY_FILE_NAME_MISSING";
    public const string ManifestEntryTypeNameMissing = "MANIFEST_ENTRY_TYPE_NAME_MISSING";
    public const string ManifestAssemblyFileNameMismatch = "MANIFEST_ASSEMBLY_FILE_NAME_MISMATCH";
    public const string CompatibilityContractsReferenceMissing = "COMPATIBILITY_CONTRACTS_REFERENCE_MISSING";
    public const string CompatibilityMajorMismatch = "COMPATIBILITY_CONTRACTS_MAJOR_MISMATCH";
    public const string LifecycleEntrypointNotFound = "LIFECYCLE_ENTRYPOINT_NOT_FOUND";
    public const string LifecycleEntrypointInvalid = "LIFECYCLE_ENTRYPOINT_INVALID";
    public const string LifecycleEntrypointInstantiationFailed = "LIFECYCLE_ENTRYPOINT_INSTANTIATION_FAILED";
    public const string LifecyclePluginIdMissing = "LIFECYCLE_PLUGIN_ID_MISSING";
    public const string LifecycleDisplayNameMissing = "LIFECYCLE_DISPLAY_NAME_MISSING";
}
