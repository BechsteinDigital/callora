using System.Text.Json;
using Callora.Host.Backend.Application.Abstractions.Plugins;

namespace Callora.Host.Backend.Infrastructure.Plugins;

public sealed class JsonPluginPackageRegistryReader : IPluginPackageRegistryReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async ValueTask<PluginPackageRegistryReadResult> ReadForAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            return new PluginPackageRegistryReadResult(
                HasRegistryFile: false,
                IsValid: true,
                RegistryPath: null,
                Registry: null);
        }

        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        var directory = Path.GetDirectoryName(fullAssemblyPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return new PluginPackageRegistryReadResult(false, true, null, null);
        }

        var registryPath = Path.Combine(directory, "registry.json");
        if (!File.Exists(registryPath))
        {
            return new PluginPackageRegistryReadResult(false, true, registryPath, null);
        }

        try
        {
            var json = await File.ReadAllTextAsync(registryPath, cancellationToken).ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<PluginRegistryJsonDto>(json, JsonOptions);
            if (dto is null)
                return Invalid(registryPath, "registry.json is empty.");

            if (string.IsNullOrWhiteSpace(dto.SchemaVersion))
                return Invalid(registryPath, "registry.json: 'schemaVersion' is required.");
            if (string.IsNullOrWhiteSpace(dto.Name))
                return Invalid(registryPath, "registry.json: 'name' is required.");
            if (string.IsNullOrWhiteSpace(dto.PluginId))
                return Invalid(registryPath, "registry.json: 'pluginId' is required.");
            if (string.IsNullOrWhiteSpace(dto.Version))
                return Invalid(registryPath, "registry.json: 'version' is required.");
            if (string.IsNullOrWhiteSpace(dto.AssemblyFileName))
                return Invalid(registryPath, "registry.json: 'assemblyFileName' is required.");
            if (string.IsNullOrWhiteSpace(dto.EntryTypeName))
                return Invalid(registryPath, "registry.json: 'entryTypeName' is required.");

            var metadata = new PluginPackageRegistryMetadata(
                dto.SchemaVersion,
                dto.Name,
                dto.PluginId,
                dto.Version,
                dto.AssemblyFileName,
                dto.EntryTypeName,
                dto.Capabilities ?? [],
                dto.Dependencies ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            return new PluginPackageRegistryReadResult(true, true, registryPath, metadata);
        }
        catch (JsonException ex)
        {
            return Invalid(registryPath, $"registry.json parse error: {ex.Message}");
        }
    }

    private static PluginPackageRegistryReadResult Invalid(string registryPath, string errorMessage) =>
        new(
            HasRegistryFile: true,
            IsValid: false,
            RegistryPath: registryPath,
            Registry: null,
            ErrorMessage: errorMessage);

    private sealed class PluginRegistryJsonDto
    {
        public string? SchemaVersion { get; set; }

        public string? Name { get; set; }

        public string? PluginId { get; set; }

        public string? Version { get; set; }

        public string? AssemblyFileName { get; set; }

        public string? EntryTypeName { get; set; }

        public string[]? Capabilities { get; set; }

        public Dictionary<string, string>? Dependencies { get; set; }
    }
}
