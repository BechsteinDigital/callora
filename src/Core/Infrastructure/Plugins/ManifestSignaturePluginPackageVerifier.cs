using System.Security.Cryptography;
using System.Text.Json;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Signing;
using Callora.Core.Application.Policies;

namespace Callora.Core.Infrastructure.Plugins;

/// <summary>
/// Verifies a plugin against its <c>plugin.signature.json</c> (next to the assembly):
/// recomputes the covered file hashes, checks the ECDSA-P256 signature against the
/// trusted signer's public key, and requires the signer fingerprint to be trusted.
/// Cross-platform — replaces the Windows-only Authenticode path. An unsigned plugin
/// is rejected unless <see cref="BackendHostOptions.AllowUnsignedPlugins"/> is set.
/// </summary>
public sealed class ManifestSignaturePluginPackageVerifier(
    IPluginSignatureTrustStore trustStore,
    BackendHostOptions options) : IPluginPackageSignatureVerifier
{
    private const string SignatureFileName = "plugin.signature.json";

    public async ValueTask<PluginPackageSignatureVerificationResult> VerifyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            return Invalid("Plugin assembly path is missing or does not exist.", PluginPackageSignatureErrorCodes.InvalidSignature);
        }

        var pluginRoot = Path.GetDirectoryName(Path.GetFullPath(assemblyPath));
        if (string.IsNullOrWhiteSpace(pluginRoot))
        {
            return Invalid("Plugin root could not be resolved.", PluginPackageSignatureErrorCodes.InvalidSignature);
        }

        var signaturePath = Path.Combine(pluginRoot, SignatureFileName);
        if (!File.Exists(signaturePath))
        {
            return options.AllowUnsignedPlugins
                ? new PluginPackageSignatureVerificationResult(IsValid: true, SignerThumbprint: null)
                : Invalid("Plugin package is unsigned.", PluginPackageSignatureErrorCodes.UnsignedPackage);
        }

        PluginSignatureManifest? manifest;
        try
        {
            manifest = PluginSignatureManifestSerializer.Deserialize(
                await File.ReadAllTextAsync(signaturePath, cancellationToken).ConfigureAwait(false));
        }
        catch (JsonException)
        {
            manifest = null;
        }

        if (manifest is null ||
            manifest.Files is null ||
            manifest.Files.Count == 0 || // an empty file list covers nothing — reject
            string.IsNullOrWhiteSpace(manifest.Signature) ||
            string.IsNullOrWhiteSpace(manifest.SignerFingerprint))
        {
            return Invalid("Plugin signature manifest is malformed.", PluginPackageSignatureErrorCodes.InvalidSignature);
        }

        // The listed files must exist and hash to exactly what the manifest signed.
        foreach (var file in manifest.Files)
        {
            string absolutePath;
            try
            {
                absolutePath = PluginContentHasher.ResolveContained(pluginRoot, file.Path);
            }
            catch (ArgumentException)
            {
                return Invalid(
                    $"Signature manifest path '{file.Path}' escapes the plugin root.",
                    PluginPackageSignatureErrorCodes.ContentHashMismatch,
                    manifest.SignerFingerprint);
            }

            if (!File.Exists(absolutePath) ||
                !string.Equals(PluginContentHasher.HashFile(absolutePath), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return Invalid(
                    $"Content hash mismatch for '{file.Path}'.",
                    PluginPackageSignatureErrorCodes.ContentHashMismatch,
                    manifest.SignerFingerprint);
            }
        }

        var publicKeyPem = trustStore.ResolvePublicKeyPem(manifest.SignerFingerprint);
        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            return Invalid(
                "Plugin package signer is not trusted.",
                PluginPackageSignatureErrorCodes.UntrustedSigner,
                manifest.SignerFingerprint);
        }

        using var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(publicKeyPem);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            return Invalid(
                "Trusted signer public key could not be loaded.",
                PluginPackageSignatureErrorCodes.InvalidSignature,
                manifest.SignerFingerprint);
        }

        var canonical = PluginSignatureManifestSerializer.SerializeCanonical(manifest);
        if (!PluginSignatureCryptography.Verify(canonical, manifest.Signature, key))
        {
            return Invalid(
                "Plugin package signature is invalid.",
                PluginPackageSignatureErrorCodes.InvalidSignature,
                manifest.SignerFingerprint);
        }

        return new PluginPackageSignatureVerificationResult(IsValid: true, SignerThumbprint: manifest.SignerFingerprint);
    }

    private static PluginPackageSignatureVerificationResult Invalid(string message, string errorCode, string? signerThumbprint = null) =>
        new(IsValid: false, ErrorMessage: message, ErrorCode: errorCode, SignerThumbprint: signerThumbprint);
}
