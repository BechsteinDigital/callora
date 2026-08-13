namespace Callora.Core.Application.Plugins;

/// <param name="Tier">
/// Die Stufe aus dem Manifest, unaufgelöst ("system"/"application"/etwas anderes).
/// <para>
/// Roh und nicht als <see cref="PluginTier"/>, weil erst der Leser weiß, aus welchem Verzeichnis
/// das Paket kam — und das Verzeichnis ist der Vorgabewert, wenn die Angabe fehlt oder unbekannt
/// ist (<see cref="PluginTierResolver"/>). Ohne dieses Feld sah der Aktivierungsplaner die Angabe
/// nie: Der Leser parste sie aus der registry.json und ließ sie hier fallen.
/// </para>
/// </param>
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
    IReadOnlyList<string>? ConditionalCapabilities = null,
    string? Tier = null);
