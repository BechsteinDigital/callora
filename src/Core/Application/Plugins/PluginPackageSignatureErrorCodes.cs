namespace Callora.Core.Application.Plugins;

public static class PluginPackageSignatureErrorCodes
{
    public const string UnsignedPackage = "PLUGIN_PACKAGE_UNSIGNED";
    public const string InvalidSignature = "PLUGIN_PACKAGE_SIGNATURE_INVALID";
    public const string UntrustedSigner = "PLUGIN_PACKAGE_SIGNER_UNTRUSTED";
    public const string ContentHashMismatch = "PLUGIN_PACKAGE_CONTENT_HASH_MISMATCH";
    public const string Revoked = "PLUGIN_PACKAGE_REVOKED";
}
