using Callora.Core.Application.Plugins;
using Callora.Core.Application.Security;
using Callora.Core.Domain.Extensions;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Plugins;

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
            {
                return Invalid(registryPath, "registry.json is empty.");
            }

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
            {
                return Invalid(registryPath, "registry.json: 'schemaVersion' is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Invalid(registryPath, "registry.json: 'name' is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.PluginId))
            {
                return Invalid(registryPath, "registry.json: 'pluginId' is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Version))
            {
                return Invalid(registryPath, "registry.json: 'version' is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.AssemblyFileName))
            {
                return Invalid(registryPath, "registry.json: 'assemblyFileName' is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.EntryTypeName))
            {
                return Invalid(registryPath, "registry.json: 'entryTypeName' is required.");
            }

            var extensions = new List<PluginPackageExtensionRegistration>();
            if (dto.Extensions is not null)
            {
                for (var i = 0; i < dto.Extensions.Length; i++)
                {
                    var extension = dto.Extensions[i];

                    // Ein Eintrag, der GAR NICHTS nennt, wird weiterhin übersprungen: ein leeres
                    // Array-Element ist unordentlich, aber kein Vertipper — es gibt niemanden zu
                    // warnen. Ein Eintrag, der etwas Falsches nennt, ist das Gegenteil.
                    if (extension is null ||
                        (string.IsNullOrWhiteSpace(extension.ExtensionPointId) &&
                         string.IsNullOrWhiteSpace(extension.Surface)))
                    {
                        continue;
                    }

                    // Dieselben zwei Prüfungen, die PluginExtensionSynchronizer zur Laufzeit
                    // macht — nur früher. Vorher übersprang dieser Pfad sie stillschweigend:
                    // "workspace.navigation.mian" installierte, aktivierte, meldete sich gesund
                    // und tauchte einfach nicht auf. Dass CAL0004 im CODE genau diesen Vertipper
                    // verhindert, machte es schlimmer, nicht besser — die Regel bewachte die Tür
                    // und ließ das Fenster offen.
                    var extensionPointId = extension.ExtensionPointId?.Trim() ?? string.Empty;
                    if (extensionPointId.Length == 0)
                    {
                        return Invalid(
                            registryPath,
                            "registry.json: an extension entry names a surface but no 'extensionPointId'.",
                            PluginRegistryErrorCodes.ExtensionPointIdMissing);
                    }

                    if (string.IsNullOrWhiteSpace(extension.Surface))
                    {
                        return Invalid(
                            registryPath,
                            $"registry.json: extension '{extensionPointId}' names no 'surface'.",
                            PluginRegistryErrorCodes.ExtensionSurfaceMissing);
                    }

                    if (!KnownExtensionPointIds.Contains(extensionPointId))
                    {
                        return Invalid(
                            registryPath,
                            $"registry.json: extension point '{extensionPointId}' does not exist.",
                            PluginRegistryErrorCodes.ExtensionPointUnknown);
                    }

                    if (!ExtensionSurfaceCodes.TryParse(extension.Surface, out var surface))
                    {
                        return Invalid(
                            registryPath,
                            $"registry.json: extension surface '{extension.Surface}' for '{extensionPointId}' is not a known surface.",
                            PluginRegistryErrorCodes.ExtensionSurfaceInvalid);
                    }

                    extensions.Add(new PluginPackageExtensionRegistration(extensionPointId, surface));
                }
            }

            // Ein nicht deklarierbarer Schlüssel macht das ganze Manifest ungültig, statt
            // übersprungen zu werden. Überspringen setzte das Plugin genau in den Zustand
            // zurück, den diese Deklaration behebt: installiert, 403 liefernd, und der Grund
            // zwei Schichten tiefer.
            var declaredPermissions = new List<PluginDeclaredPermission>();
            var seenPermissionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var permission in dto.Permissions ?? [])
            {
                var key = permission?.Key?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!PluginPermissionKeyPolicy.IsDeclarable(dto.PluginId, key, out var reason))
                {
                    return Invalid(
                        registryPath,
                        $"registry.json: {reason}",
                        PluginRegistryErrorCodes.PermissionNotDeclarable);
                }

                // Wiederholung ist unordentlich, nicht gefährlich — eine Installation daran
                // scheitern zu lassen wäre ein schlechter Tausch für den, der davorsteht.
                if (seenPermissionKeys.Add(key))
                {
                    declaredPermissions.Add(new PluginDeclaredPermission(
                        key,
                        string.IsNullOrWhiteSpace(permission!.Description) ? null : permission.Description.Trim()));
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
                .ToArray(),
                (dto.ConditionalCapabilities ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
                dto.Tier,
                declaredPermissions);

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

    /// <summary>
    /// Die Erweiterungspunkte, die es gibt — aus derselben Quelle, aus der auch
    /// <c>InMemoryExtensionPointRegistryStore</c> sich füllt. Eine zweite Liste hier wäre genau
    /// die Stelle, an der Manifest-Prüfung und Laufzeit-Prüfung auseinanderlaufen.
    /// </summary>
    private static readonly HashSet<string> KnownExtensionPointIds =
        Callora.Core.Infrastructure.Extensions.BackendExtensionPointCatalog.Build()
            .Select(definition => definition.ExtensionPointId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
