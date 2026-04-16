using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Application.Abstractions.Plugins;
using Callora.Host.Backend.Application.Audit;
using Callora.Host.Backend.Application.Events;
using Callora.Host.Backend.Domain.Plugins;
using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

public sealed class PluginLifecycleService(
    IHostPluginLifecycle lifecycle,
    IPluginActivationPolicy activationPolicy,
    IHostAuditStore auditStore,
    IPluginInstallationRepository installationRepository,
    IHostUnitOfWork unitOfWork,
    IPluginPackageRegistryReader packageRegistryReader,
    INuGetPluginAssemblyResolver nuGetAssemblyResolver,
    IHostApplicationEventPublisher eventPublisher) : IPluginLifecycleService
{
    public IReadOnlyCollection<HostPluginDescriptor> Plugins => lifecycle.Plugins;

    public async Task<IReadOnlyList<PluginInstallationSnapshot>> GetInstallationsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(x => new PluginInstallationSnapshot(
                x.PluginId,
                x.DisplayName,
                x.AssemblyPath,
                x.EntryTypeName,
                (int)x.State,
                x.InstalledAtUtc,
                x.UpdatedAtUtc))
            .ToArray();
    }

    public async Task<PluginLifecycleServiceResult> InstallAsync(
        InstallPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        return await InstallFromResolvedAssemblyAsync(
                assemblyPath: command.AssemblyPath,
                requestedEntryTypeName: command.EntryTypeName,
                requestedBy: command.RequestedBy,
                sourceMetadata: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PluginLifecycleServiceResult> InstallFromNuGetAsync(
        InstallNuGetPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        var resolved = await nuGetAssemblyResolver
            .ResolveAsync(command.PackageId, command.PackageVersion, command.AssemblyFileName, cancellationToken)
            .ConfigureAwait(false);
        if (!resolved.IsSuccess || string.IsNullOrWhiteSpace(resolved.AssemblyPath))
        {
            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                null,
                resolved.Message ?? "NuGet package resolve failed.");
        }

        var metadata = new Dictionary<string, string>
        {
            ["packageId"] = command.PackageId,
            ["packageVersion"] = command.PackageVersion,
            ["assemblyFileName"] = command.AssemblyFileName ?? string.Empty
        };

        return await InstallFromResolvedAssemblyAsync(
                assemblyPath: resolved.AssemblyPath,
                requestedEntryTypeName: command.EntryTypeName,
                requestedBy: command.RequestedBy,
                sourceMetadata: metadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PluginLifecycleServiceResult> InstallFromResolvedAssemblyAsync(
        string assemblyPath,
        string? requestedEntryTypeName,
        string? requestedBy,
        IReadOnlyDictionary<string, string>? sourceMetadata,
        CancellationToken cancellationToken)
    {
        var packageRead = await packageRegistryReader
            .ReadForAssemblyAsync(assemblyPath, cancellationToken)
            .ConfigureAwait(false);
        if (packageRead.HasRegistryFile && !packageRead.IsValid)
        {
            await PublishLifecycleEventAsync(
                    action: "plugin.install",
                    pluginId: null,
                    isSuccess: false,
                    requestedBy: requestedBy,
                    message: packageRead.ErrorMessage,
                    metadata: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                null,
                packageRead.ErrorMessage);
        }

        var package = packageRead.Registry;
        if (package is not null &&
            !string.Equals(Path.GetFileName(assemblyPath), package.AssemblyFileName, StringComparison.Ordinal))
        {
            var mismatchMessage = $"registry.json expects assembly '{package.AssemblyFileName}', but request uses '{Path.GetFileName(assemblyPath)}'.";
            await PublishLifecycleEventAsync(
                    action: "plugin.install",
                    pluginId: null,
                    isSuccess: false,
                    requestedBy: requestedBy,
                    message: mismatchMessage,
                    metadata: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                null,
                mismatchMessage);
        }

        var effectiveEntryTypeName = string.IsNullOrWhiteSpace(requestedEntryTypeName)
            ? package?.EntryTypeName
            : requestedEntryTypeName;

        var result = await lifecycle.InstallAsync(assemblyPath, effectiveEntryTypeName, cancellationToken)
            .ConfigureAwait(false);

        var installMetadata = new Dictionary<string, string>
        {
            ["assemblyPath"] = assemblyPath,
            ["entryTypeName"] = effectiveEntryTypeName ?? string.Empty
        };
        if (sourceMetadata is not null)
        {
            foreach (var (key, value) in sourceMetadata)
                installMetadata[key] = value;
        }
        if (package is not null)
        {
            installMetadata["registryPath"] = packageRead.RegistryPath ?? string.Empty;
            installMetadata["registryPluginId"] = package.PluginId;
            installMetadata["registryVersion"] = package.Version;
            installMetadata["registryName"] = package.Name;
        }

        await auditStore.WritePluginAuditAsync(
                action: "plugin.install",
                pluginId: result.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: requestedBy,
                message: result.Message,
                metadata: installMetadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await PublishLifecycleEventAsync(
                action: "plugin.install",
                pluginId: result.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: requestedBy,
                message: result.Message,
                metadata: installMetadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess &&
            package is not null &&
            !string.IsNullOrWhiteSpace(result.PluginId) &&
            !string.Equals(result.PluginId, package.PluginId, StringComparison.OrdinalIgnoreCase))
        {
            _ = await lifecycle.UninstallAsync(result.PluginId, cancellationToken).ConfigureAwait(false);

            var mismatchMessage = $"registry.json pluginId '{package.PluginId}' does not match runtime pluginId '{result.PluginId}'.";
            await PublishLifecycleEventAsync(
                    action: "plugin.install",
                    pluginId: result.PluginId,
                    isSuccess: false,
                    requestedBy: requestedBy,
                    message: mismatchMessage,
                    metadata: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                result.PluginId,
                mismatchMessage);
        }

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.PluginId))
        {
            var descriptor = FindDescriptor(result.PluginId);
            await UpsertInstalledAsync(
                    pluginId: result.PluginId,
                    displayName: descriptor?.DisplayName ?? result.PluginId,
                    assemblyPath: descriptor?.AssemblyPath ?? assemblyPath,
                    entryTypeName: descriptor?.EntryTypeName ?? effectiveEntryTypeName,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return result.IsSuccess
            ? new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.Ok, true, result.PluginId, result.Message)
            : new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, result.PluginId, result.Message);
    }

    public async Task<PluginLifecycleServiceResult> ActivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        var decision = await activationPolicy.EvaluateAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
        if (!decision.IsAllowed)
        {
            await auditStore.WritePluginAuditAsync(
                    action: "plugin.activate",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: decision.Reason,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await PublishLifecycleEventAsync(
                    action: "plugin.activate",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: decision.Reason,
                    metadata: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.Forbidden,
                false,
                command.PluginId,
                decision.Reason);
        }

        var result = await lifecycle.ActivateAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
        await auditStore.WritePluginAuditAsync(
                action: "plugin.activate",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await PublishLifecycleEventAsync(
                action: "plugin.activate",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                metadata: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            var descriptor = FindDescriptor(command.PluginId);
            await UpsertInstallationAsync(
                    pluginId: command.PluginId,
                    displayName: descriptor?.DisplayName ?? command.PluginId,
                    assemblyPath: descriptor?.AssemblyPath ?? string.Empty,
                    entryTypeName: descriptor?.EntryTypeName,
                    mark: static (x, now) => x.MarkActivated(now),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return result.IsSuccess
            ? new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.Ok, true, command.PluginId, result.Message)
            : new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, command.PluginId, result.Message);
    }

    public async Task<PluginLifecycleServiceResult> DeactivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await lifecycle.DeactivateAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
        await auditStore.WritePluginAuditAsync(
                action: "plugin.deactivate",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await PublishLifecycleEventAsync(
                action: "plugin.deactivate",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                metadata: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            var descriptor = FindDescriptor(command.PluginId);
            await UpsertInstallationAsync(
                    pluginId: command.PluginId,
                    displayName: descriptor?.DisplayName ?? command.PluginId,
                    assemblyPath: descriptor?.AssemblyPath ?? string.Empty,
                    entryTypeName: descriptor?.EntryTypeName,
                    mark: static (x, now) => x.MarkDeactivated(now),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return result.IsSuccess
            ? new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.Ok, true, command.PluginId, result.Message)
            : new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, command.PluginId, result.Message);
    }

    public async Task<PluginLifecycleServiceResult> UninstallAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await lifecycle.UninstallAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
        await auditStore.WritePluginAuditAsync(
                action: "plugin.uninstall",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await PublishLifecycleEventAsync(
                action: "plugin.uninstall",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                metadata: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            var descriptor = FindDescriptor(command.PluginId);
            await UpsertInstallationAsync(
                    pluginId: command.PluginId,
                    displayName: descriptor?.DisplayName ?? command.PluginId,
                    assemblyPath: descriptor?.AssemblyPath ?? string.Empty,
                    entryTypeName: descriptor?.EntryTypeName,
                    mark: static (x, now) => x.MarkUninstalled(now),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return result.IsSuccess
            ? new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.Ok, true, command.PluginId, result.Message)
            : new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, command.PluginId, result.Message);
    }

    private HostPluginDescriptor? FindDescriptor(string pluginId)
    {
        foreach (var plugin in lifecycle.Plugins)
        {
            if (string.Equals(plugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
                return plugin;
        }

        return null;
    }

    private async Task UpsertInstallationAsync(
        string pluginId,
        string displayName,
        string assemblyPath,
        string? entryTypeName,
        Action<PluginInstallation, DateTimeOffset> mark,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var installation = await installationRepository.GetByPluginIdAsync(pluginId, cancellationToken).ConfigureAwait(false);

        if (installation is null)
        {
            var safeAssemblyPath = string.IsNullOrWhiteSpace(assemblyPath) ? "unknown" : assemblyPath;
            installation = PluginInstallation.CreateInstalled(
                pluginId,
                displayName,
                safeAssemblyPath,
                entryTypeName,
                now);

            await installationRepository.AddAsync(installation, cancellationToken).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            installation.ApplyInstallMetadata(displayName, assemblyPath, entryTypeName, now);
        }

        mark(installation, now);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertInstalledAsync(
        string pluginId,
        string displayName,
        string assemblyPath,
        string? entryTypeName,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var installation = await installationRepository.GetByPluginIdAsync(pluginId, cancellationToken).ConfigureAwait(false);

        if (installation is null)
        {
            installation = PluginInstallation.CreateInstalled(
                pluginId,
                displayName,
                assemblyPath,
                entryTypeName,
                now);

            await installationRepository.AddAsync(installation, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            installation.ApplyInstallMetadata(displayName, assemblyPath, entryTypeName, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task PublishLifecycleEventAsync(
        string action,
        string? pluginId,
        bool isSuccess,
        string? requestedBy,
        string? message,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken) =>
        eventPublisher.PublishAsync(
            new PluginLifecycleChangedEvent(
                DateTimeOffset.UtcNow,
                action,
                pluginId,
                isSuccess,
                requestedBy,
                message,
                metadata),
            cancellationToken);
}
