using Callora.Core.Application.Extensions;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Installs plugin assemblies after validating registry metadata and package signatures.
/// </summary>
public sealed class PluginInstaller(
    IHostPluginLifecycle lifecycle,
    IPluginPackageRegistryReader packageRegistryReader,
    IPluginPackageSignatureVerifier packageSignatureVerifier,
    INuGetPluginAssemblyResolver nuGetAssemblyResolver,
    IPluginExtensionRegistrationStore extensionRegistrationStore,
    PluginLifecycleReporter reporter,
    PluginInstallationRecorder recorder)
{
    /// <summary>
    /// Resolves one NuGet package and installs the contained plugin assembly.
    /// </summary>
    public async Task<PluginLifecycleServiceResult> InstallFromNuGetAsync(
        InstallNuGetPluginCommand command,
        CancellationToken cancellationToken)
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

    /// <summary>
    /// Installs one already resolved plugin assembly through all validation gates.
    /// </summary>
    public async Task<PluginLifecycleServiceResult> InstallFromResolvedAssemblyAsync(
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
            var reasonCode = PluginLifecycleCodeMap.MapPackageErrorCode(packageRead.ErrorCode)
                ?? PluginLifecycleErrorCodes.PluginRegistryInvalid;
            await reporter.ReportInstallGateRejectAsync(
                    pluginId: null,
                    requestedBy: requestedBy,
                    message: packageRead.ErrorMessage,
                    gateType: "registry.validation",
                    reasonCode: reasonCode,
                    assemblyPath: assemblyPath,
                    additionalMetadata: packageRead.RegistryPath is null
                        ? null
                        : new Dictionary<string, string>
                        {
                            ["registryPath"] = packageRead.RegistryPath
                        },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                null,
                packageRead.ErrorMessage,
                reasonCode);
        }

        var package = packageRead.Registry;
        if (package is not null &&
            !string.Equals(Path.GetFileName(assemblyPath), package.AssemblyFileName, StringComparison.Ordinal))
        {
            var mismatchMessage = $"registry.json expects assembly '{package.AssemblyFileName}', but request uses '{Path.GetFileName(assemblyPath)}'.";
            await reporter.ReportInstallGateRejectAsync(
                    pluginId: null,
                    requestedBy: requestedBy,
                    message: mismatchMessage,
                    gateType: "registry.assembly_match",
                    reasonCode: PluginLifecycleErrorCodes.PluginAssemblyFileNameMismatch,
                    assemblyPath: assemblyPath,
                    additionalMetadata: new Dictionary<string, string>
                    {
                        ["registryAssemblyFileName"] = package.AssemblyFileName,
                        ["requestedAssemblyFileName"] = Path.GetFileName(assemblyPath)
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                null,
                mismatchMessage,
                PluginLifecycleErrorCodes.PluginAssemblyFileNameMismatch);
        }

        var effectiveEntryTypeName = string.IsNullOrWhiteSpace(requestedEntryTypeName)
            ? package?.EntryTypeName
            : requestedEntryTypeName;

        var signatureVerification = await packageSignatureVerifier
            .VerifyAsync(assemblyPath, cancellationToken)
            .ConfigureAwait(false);
        if (!signatureVerification.IsValid)
        {
            var signatureErrorCode = PluginLifecycleCodeMap.MapSignatureErrorCode(signatureVerification.ErrorCode)
                ?? PluginLifecycleErrorCodes.PluginPackageSignatureInvalid;
            var signatureMetadata = new Dictionary<string, string>
            {
                ["assemblyPath"] = assemblyPath,
                ["signatureErrorCode"] = signatureErrorCode
            };
            if (!string.IsNullOrWhiteSpace(signatureVerification.SignerThumbprint))
            {
                signatureMetadata["signatureSignerThumbprint"] = signatureVerification.SignerThumbprint;
            }

            await reporter.ReportInstallGateRejectAsync(
                    pluginId: null,
                    requestedBy: requestedBy,
                    message: signatureVerification.ErrorMessage,
                    gateType: "signature.validation",
                    reasonCode: signatureErrorCode,
                    assemblyPath: assemblyPath,
                    additionalMetadata: signatureMetadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                null,
                signatureVerification.ErrorMessage,
                signatureErrorCode);
        }

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
            installMetadata["registryContractVersion"] = package.ContractVersion;
        }
        if (!string.IsNullOrWhiteSpace(packageRead.WarningMessage))
        {
            installMetadata["registryWarning"] = packageRead.WarningMessage;
        }
        if (!string.IsNullOrWhiteSpace(packageRead.WarningCode))
        {
            installMetadata["registryWarningCode"] = packageRead.WarningCode;
        }
        if (!string.IsNullOrWhiteSpace(signatureVerification.SignerThumbprint))
        {
            installMetadata["signatureSignerThumbprint"] = signatureVerification.SignerThumbprint;
        }

        await reporter.ReportAsync(
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
            await extensionRegistrationStore.RemoveAsync(result.PluginId, cancellationToken).ConfigureAwait(false);

            var mismatchMessage = $"registry.json pluginId '{package.PluginId}' does not match runtime pluginId '{result.PluginId}'.";
            await reporter.ReportInstallGateRejectAsync(
                    pluginId: result.PluginId,
                    requestedBy: requestedBy,
                    message: mismatchMessage,
                    gateType: "registry.plugin_id_match",
                    reasonCode: PluginLifecycleErrorCodes.PluginRegistryPluginIdMismatch,
                    assemblyPath: assemblyPath,
                    additionalMetadata: new Dictionary<string, string>
                    {
                        ["registryPluginId"] = package.PluginId,
                        ["runtimePluginId"] = result.PluginId
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                result.PluginId,
                mismatchMessage,
                PluginLifecycleErrorCodes.PluginRegistryPluginIdMismatch);
        }

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.PluginId))
        {
            // Code-first extension wiring is synced on runtime activation.
            await extensionRegistrationStore.RemoveAsync(result.PluginId, cancellationToken).ConfigureAwait(false);

            var descriptor = lifecycle.FindDescriptor(result.PluginId);
            await recorder.RecordInstalledAsync(
                    pluginId: result.PluginId,
                    displayName: descriptor?.DisplayName ?? result.PluginId,
                    assemblyPath: descriptor?.AssemblyPath ?? assemblyPath,
                    entryTypeName: descriptor?.EntryTypeName ?? effectiveEntryTypeName,
                    cancellationToken: cancellationToken,
                    providedCapabilities: package?.Capabilities,
                    requiredCapabilities: package?.RequiredCapabilities)
                .ConfigureAwait(false);
        }

        return result.IsSuccess
            ? new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.Ok,
                true,
                result.PluginId,
                result.Message,
                WarningMessage: packageRead.WarningMessage,
                WarningCode: PluginLifecycleCodeMap.MapPackageWarningCode(packageRead.WarningCode))
            : new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                result.PluginId,
                result.Message,
                WarningMessage: packageRead.WarningMessage,
                WarningCode: PluginLifecycleCodeMap.MapPackageWarningCode(packageRead.WarningCode));
    }
}
