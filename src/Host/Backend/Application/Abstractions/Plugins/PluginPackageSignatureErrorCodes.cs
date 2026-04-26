namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public static class PluginPackageSignatureErrorCodes
{
    public const string UnsignedPackage = "PLUGIN_PACKAGE_UNSIGNED";
    public const string InvalidSignature = "PLUGIN_PACKAGE_SIGNATURE_INVALID";
    public const string UntrustedSigner = "PLUGIN_PACKAGE_SIGNER_UNTRUSTED";
}
