using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Application.Abstractions.Plugins;
using Callora.Host.Backend.Domain.Extensions;
using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Hosting.Application.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

/// <summary>
/// Validates and persists code-first extension registrations exposed by active plugins.
/// </summary>
public sealed class PluginExtensionSynchronizer(
    ICalloraPluginCatalog pluginCatalog,
    IExtensionPointRegistryStore extensionPointRegistryStore,
    IPluginExtensionRegistrationStore extensionRegistrationStore)
{
    /// <summary>
    /// Syncs the runtime extension registrations of one plugin into the registration store.
    /// </summary>
    public async Task<ExtensionSyncResult> SyncAsync(string pluginId, CancellationToken cancellationToken)
    {
        var contributors = pluginCatalog.GetExports<IHostPluginExtensionContributor>()
            .Where(x => string.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (contributors.Length == 0)
        {
            await extensionRegistrationStore.RemoveAsync(pluginId, cancellationToken).ConfigureAwait(false);
            return ExtensionSyncResult.Success;
        }

        var capabilities = contributors
            .SelectMany(x => x.Capabilities)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var registrations = new List<PluginPackageExtensionRegistration>();
        foreach (var contributor in contributors)
        {
            var contributorRegistrations = contributor.GetRegistrations();
            var validation = await ValidateAsync(contributorRegistrations, capabilities, cancellationToken)
                .ConfigureAwait(false);
            if (!validation.IsSuccess)
                return validation;

            foreach (var registration in contributorRegistrations)
            {
                if (!ExtensionSurfaceCodes.TryParse(registration.Surface, out var surface))
                {
                    return new ExtensionSyncResult(
                        false,
                        $"Runtime extension registration for '{registration.ExtensionPointId}' has invalid surface '{registration.Surface}'.",
                        PluginLifecycleErrorCodes.PluginExtensionSurfaceMismatch,
                        new Dictionary<string, string>
                        {
                            ["extensionPointId"] = registration.ExtensionPointId,
                            ["extensionSurface"] = registration.Surface
                        });
                }

                registrations.Add(new PluginPackageExtensionRegistration(
                    registration.ExtensionPointId.Trim(),
                    surface));
            }
        }

        await extensionRegistrationStore
            .UpsertAsync(
                pluginId,
                registrations,
                capabilities,
                cancellationToken)
            .ConfigureAwait(false);

        return ExtensionSyncResult.Success;
    }

    private async Task<ExtensionSyncResult> ValidateAsync(
        IReadOnlyList<HostPluginExtensionRegistration> extensionRegistrations,
        IReadOnlyList<string> capabilities,
        CancellationToken cancellationToken)
    {
        var normalizedCapabilities = new HashSet<string>(
            capabilities
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var extensionRegistration in extensionRegistrations)
        {
            var extensionPoint = await extensionPointRegistryStore
                .FindByIdAsync(extensionRegistration.ExtensionPointId, cancellationToken)
                .ConfigureAwait(false);
            if (extensionPoint is null)
            {
                return new ExtensionSyncResult(
                    false,
                    $"Runtime extension point '{extensionRegistration.ExtensionPointId}' is not registered.",
                    PluginLifecycleErrorCodes.PluginExtensionPointUnknown,
                    new Dictionary<string, string>
                    {
                        ["extensionPointId"] = extensionRegistration.ExtensionPointId,
                        ["extensionSurface"] = extensionRegistration.Surface
                    });
            }

            if (!ExtensionSurfaceCodes.TryParse(extensionRegistration.Surface, out var surface))
            {
                return new ExtensionSyncResult(
                    false,
                    $"Runtime extension point '{extensionRegistration.ExtensionPointId}' has invalid surface '{extensionRegistration.Surface}'.",
                    PluginLifecycleErrorCodes.PluginExtensionSurfaceMismatch,
                    new Dictionary<string, string>
                    {
                        ["extensionPointId"] = extensionRegistration.ExtensionPointId,
                        ["extensionSurface"] = extensionRegistration.Surface
                    });
            }

            if (extensionPoint.Surface != surface)
            {
                return new ExtensionSyncResult(
                    false,
                    $"Runtime extension point '{extensionRegistration.ExtensionPointId}' is '{extensionPoint.Surface.ToCode()}', but plugin registers '{extensionRegistration.Surface}'.",
                    PluginLifecycleErrorCodes.PluginExtensionSurfaceMismatch,
                    new Dictionary<string, string>
                    {
                        ["extensionPointId"] = extensionRegistration.ExtensionPointId,
                        ["extensionSurface"] = extensionRegistration.Surface,
                        ["expectedSurface"] = extensionPoint.Surface.ToCode()
                    });
            }

            if (!normalizedCapabilities.Contains(extensionPoint.RequiredScope))
            {
                return new ExtensionSyncResult(
                    false,
                    $"Runtime extension point '{extensionRegistration.ExtensionPointId}' requires scope '{extensionPoint.RequiredScope}', but plugin does not declare it.",
                    PluginLifecycleErrorCodes.PluginExtensionScopeMissing,
                    new Dictionary<string, string>
                    {
                        ["extensionPointId"] = extensionRegistration.ExtensionPointId,
                        ["requiredScope"] = extensionPoint.RequiredScope
                    });
            }
        }

        return ExtensionSyncResult.Success;
    }
}
