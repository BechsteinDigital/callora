namespace Callora.Core.Application.Plugins;

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
    IReadOnlyList<PluginPackageExtensionRegistration>? ExtensionRegistrations = null,
    IReadOnlyList<string>? RequiredCapabilities = null,
    IReadOnlyList<string>? ConditionalCapabilities = null);
