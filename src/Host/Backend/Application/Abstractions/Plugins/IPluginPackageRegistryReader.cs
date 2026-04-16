namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public interface IPluginPackageRegistryReader
{
    ValueTask<PluginPackageRegistryReadResult> ReadForAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}

public sealed record PluginPackageRegistryReadResult(
    bool HasRegistryFile,
    bool IsValid,
    string? RegistryPath,
    PluginPackageRegistryMetadata? Registry,
    string? ErrorMessage = null);

public sealed record PluginPackageRegistryMetadata(
    string SchemaVersion,
    string Name,
    string PluginId,
    string Version,
    string AssemblyFileName,
    string EntryTypeName,
    IReadOnlyList<string> Capabilities,
    IReadOnlyDictionary<string, string> Dependencies);
