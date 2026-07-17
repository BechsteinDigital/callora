using Callora.Core.Application.Plugins;
using Callora.Core.Application.Policies;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Callora.Core.Infrastructure.Plugins;

public sealed class AuthenticodePluginPackageSignatureVerifier : IPluginPackageSignatureVerifier
{
    private readonly IPluginSignatureTrustStore _trustStore;
    private readonly BackendHostOptions _options;

    public AuthenticodePluginPackageSignatureVerifier(
        IPluginSignatureTrustStore trustStore,
        BackendHostOptions options)
    {
        _trustStore = trustStore;
        _options = options;
    }

    public ValueTask<PluginPackageSignatureVerificationResult> VerifyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            return ValueTask.FromResult(new PluginPackageSignatureVerificationResult(
                IsValid: false,
                ErrorMessage: "Plugin assembly path is missing or does not exist.",
                ErrorCode: PluginPackageSignatureErrorCodes.InvalidSignature));
        }

        X509Certificate2 certificate;
        try
        {
#pragma warning disable SYSLIB0057 // CreateFromSignedFile is required for Authenticode extraction from signed PE files.
            using var signedCertificate = X509Certificate.CreateFromSignedFile(assemblyPath);
#pragma warning restore SYSLIB0057
            var exportedCertificate = signedCertificate.Export(X509ContentType.Cert);
            certificate = X509CertificateLoader.LoadCertificate(exportedCertificate);
        }
        catch (CryptographicException)
        {
            if (_options.AllowUnsignedPlugins)
            {
                return ValueTask.FromResult(new PluginPackageSignatureVerificationResult(
                    IsValid: true,
                    SignerThumbprint: null));
            }

            return ValueTask.FromResult(new PluginPackageSignatureVerificationResult(
                IsValid: false,
                ErrorMessage: "Plugin package is unsigned.",
                ErrorCode: PluginPackageSignatureErrorCodes.UnsignedPackage));
        }
        catch (PlatformNotSupportedException)
        {
            return ValueTask.FromResult(new PluginPackageSignatureVerificationResult(
                IsValid: false,
                ErrorMessage: "Plugin package signature verification is not supported on this platform.",
                ErrorCode: PluginPackageSignatureErrorCodes.InvalidSignature));
        }

        using (certificate)
        using (var chain = new X509Chain())
        {
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            if (!chain.Build(certificate))
            {
                var chainError = string.Join(
                    "; ",
                    chain.ChainStatus.Select(static status => status.StatusInformation.Trim()).Where(static x => !string.IsNullOrWhiteSpace(x)));

                var message = string.IsNullOrWhiteSpace(chainError)
                    ? "Plugin package signature is invalid."
                    : $"Plugin package signature is invalid: {chainError}.";

                return ValueTask.FromResult(new PluginPackageSignatureVerificationResult(
                    IsValid: false,
                    ErrorMessage: message,
                    ErrorCode: PluginPackageSignatureErrorCodes.InvalidSignature));
            }

            if (!_trustStore.IsTrusted(certificate.Thumbprint))
            {
                return ValueTask.FromResult(new PluginPackageSignatureVerificationResult(
                    IsValid: false,
                    ErrorMessage: "Plugin package signer is not trusted.",
                    ErrorCode: PluginPackageSignatureErrorCodes.UntrustedSigner,
                    SignerThumbprint: certificate.Thumbprint));
            }

            return ValueTask.FromResult(new PluginPackageSignatureVerificationResult(
                IsValid: true,
                SignerThumbprint: certificate.Thumbprint));
        }
    }
}
