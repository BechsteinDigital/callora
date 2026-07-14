using System.Text.Json;
using Callora.Host.Backend.Application.Plugins;
using Callora.Host.Backend.Domain.Extensions;

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

        var registryPath = ResolveRegistryPath(directory);
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

            if (string.IsNullOrWhiteSpace(dto.ContractVersion))
            {
                return Invalid(
                    registryPath,
                    "registry.json: 'contractVersion' is required.",
                    PluginRegistryErrorCodes.ContractVersionMissing);
            }

            if (!PluginContractVersionPolicy.TryGet(dto.ContractVersion, out var contractPolicy))
            {
                return Invalid(
                    registryPath,
                    $"registry.json: unsupported contractVersion '{dto.ContractVersion}'.",
                    PluginRegistryErrorCodes.ContractVersionUnsupported);
            }

            if (contractPolicy.Status is PluginContractSupportStatus.Removed)
            {
                return Invalid(
                    registryPath,
                    $"registry.json: removed contractVersion '{dto.ContractVersion}' is no longer accepted.",
                    PluginRegistryErrorCodes.ContractVersionRemoved);
            }

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

            var extensions = new List<PluginPackageExtensionRegistration>();
            if (dto.Extensions is not null)
            {
                for (var i = 0; i < dto.Extensions.Length; i++)
                {
                    var extension = dto.Extensions[i];
                    if (extension is null || string.IsNullOrWhiteSpace(extension.ExtensionPointId))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(extension.Surface))
                    {
                        continue;
                    }

                    if (!ExtensionSurfaceCodes.TryParse(extension.Surface, out var surface))
                    {
                        continue;
                    }

                    extensions.Add(new PluginPackageExtensionRegistration(
                        extension.ExtensionPointId.Trim(),
                        surface));
                }
            }

            var metadata = new PluginPackageRegistryMetadata(
                dto.ContractVersion,
                dto.SchemaVersion,
                dto.Name,
                dto.PluginId,
                dto.Version,
                dto.AssemblyFileName,
                dto.EntryTypeName,
                (dto.Capabilities ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
                dto.Dependencies ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                extensions,
                (dto.RequiresCapabilities ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());

            var warningMessage = contractPolicy.Status is PluginContractSupportStatus.Deprecated
                ? $"registry.json: contractVersion '{dto.ContractVersion}' is deprecated and will be removed in a future release."
                : null;
            var warningCode = warningMessage is null
                ? null
                : PluginRegistryErrorCodes.ContractVersionDeprecated;

            return new PluginPackageRegistryReadResult(
                HasRegistryFile: true,
                IsValid: true,
                RegistryPath: registryPath,
                Registry: metadata,
                WarningMessage: warningMessage,
                WarningCode: warningCode);
        }
        catch (JsonException ex)
        {
            return Invalid(registryPath, $"registry.json parse error: {ex.Message}");
        }
    }

    private static PluginPackageRegistryReadResult Invalid(
        string registryPath,
        string errorMessage,
        string? errorCode = null) =>
        new(
            HasRegistryFile: true,
            IsValid: false,
            RegistryPath: registryPath,
            Registry: null,
            ErrorMessage: errorMessage,
            ErrorCode: errorCode);

    private static string ResolveRegistryPath(string assemblyDirectory)
    {
        var current = new DirectoryInfo(assemblyDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "registry.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(assemblyDirectory, "registry.json");
    }
}
