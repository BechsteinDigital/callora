using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Maps package and signature error codes to lifecycle error/warning codes.
/// </summary>
public static class PluginLifecycleCodeMap
{
    /// <summary>
    /// Maps one package registry error code to its lifecycle error code.
    /// </summary>
    public static string? MapPackageErrorCode(string? packageErrorCode) =>
        packageErrorCode switch
        {
            PluginRegistryErrorCodes.ContractVersionUnsupported => PluginLifecycleErrorCodes.PluginContractVersionUnsupported,
            PluginRegistryErrorCodes.ContractVersionRemoved => PluginLifecycleErrorCodes.PluginContractVersionRemoved,
            _ => packageErrorCode,
        };

    /// <summary>
    /// Maps one package registry warning code to its lifecycle warning code.
    /// </summary>
    public static string? MapPackageWarningCode(string? packageWarningCode) =>
        packageWarningCode switch
        {
            PluginRegistryErrorCodes.ContractVersionDeprecated => PluginLifecycleWarningCodes.PluginContractVersionDeprecated,
            _ => packageWarningCode,
        };

    /// <summary>
    /// Maps one signature verification error code to its lifecycle error code.
    /// </summary>
    public static string? MapSignatureErrorCode(string? signatureErrorCode) =>
        signatureErrorCode switch
        {
            PluginPackageSignatureErrorCodes.UnsignedPackage => PluginLifecycleErrorCodes.PluginPackageUnsigned,
            PluginPackageSignatureErrorCodes.InvalidSignature => PluginLifecycleErrorCodes.PluginPackageSignatureInvalid,
            PluginPackageSignatureErrorCodes.UntrustedSigner => PluginLifecycleErrorCodes.PluginPackageSignerUntrusted,
            _ => signatureErrorCode,
        };
}
