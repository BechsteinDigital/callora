using System.Security.Cryptography;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Signing;
using Callora.Core.Application.Policies;

namespace Callora.Core.Infrastructure.Plugins;

/// <summary>
/// Trusted plugin signers from configuration. A signer with a public key (PEM) is
/// key-resolvable: its fingerprint (SHA-256 of the SubjectPublicKeyInfo) is derived
/// and its key can verify an ECDSA manifest signature. Legacy thumbprint-only
/// entries are listed for visibility but resolve to no key — fail-closed.
/// </summary>
public sealed class ConfiguredPluginSignatureTrustStore : IPluginSignatureTrustStore
{
    private readonly HashSet<string> _trustedFingerprints;
    private readonly Dictionary<string, string> _publicKeyPemByFingerprint;
    private readonly TrustedPluginSigner[] _trustedSigners;

    public ConfiguredPluginSignatureTrustStore(BackendHostOptions options)
    {
        _trustedFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _publicKeyPemByFingerprint = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var signers = new List<TrustedPluginSigner>();

        // Legacy thumbprint-only entries (Authenticode era): listed, but cannot
        // verify an ECDSA manifest signature — no key means deny (fail-closed).
        foreach (var thumbprint in options.TrustedSignerThumbprints)
        {
            var normalized = Normalize(thumbprint);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            _trustedFingerprints.Add(normalized);
            signers.Add(new TrustedPluginSigner(
                PublisherId: "legacy",
                DisplayName: "Legacy Configured Signer",
                Thumbprint: normalized,
                Source: "backendHost.trustedSignerThumbprints"));
        }

        foreach (var signer in options.TrustedSigners)
        {
            if (!string.IsNullOrWhiteSpace(signer.PublicKey))
            {
                var fingerprint = TryComputeFingerprint(signer.PublicKey);
                if (fingerprint is null)
                {
                    // An unparseable configured key is ignored — fail-closed.
                    continue;
                }

                _trustedFingerprints.Add(fingerprint);
                _publicKeyPemByFingerprint[fingerprint] = signer.PublicKey;
                signers.Add(new TrustedPluginSigner(
                    Coalesce(signer.PublisherId, "unknown"),
                    Coalesce(signer.DisplayName, signer.PublisherId),
                    fingerprint,
                    Coalesce(signer.Source, "backendHost.trustedSigners")));
                continue;
            }

            var normalizedThumbprint = Normalize(signer.Thumbprint);
            if (string.IsNullOrWhiteSpace(normalizedThumbprint))
            {
                continue;
            }

            _trustedFingerprints.Add(normalizedThumbprint);
            signers.Add(new TrustedPluginSigner(
                Coalesce(signer.PublisherId, "unknown"),
                Coalesce(signer.DisplayName, signer.PublisherId),
                normalizedThumbprint,
                Coalesce(signer.Source, "backendHost.trustedSigners")));
        }

        _trustedSigners = signers
            .DistinctBy(static x => x.Thumbprint, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool IsTrusted(string? signerThumbprint)
    {
        var normalized = Normalize(signerThumbprint);
        return !string.IsNullOrWhiteSpace(normalized) && _trustedFingerprints.Contains(normalized);
    }

    public string? ResolvePublicKeyPem(string? signerFingerprint)
    {
        var normalized = Normalize(signerFingerprint);
        return !string.IsNullOrWhiteSpace(normalized) && _publicKeyPemByFingerprint.TryGetValue(normalized, out var pem)
            ? pem
            : null;
    }

    public IReadOnlyList<TrustedPluginSigner> GetTrustedSigners() => _trustedSigners;

    private static string? TryComputeFingerprint(string publicKeyPem)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(publicKeyPem);
            return PluginSignatureCryptography.ComputeFingerprint(key);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            return null;
        }
    }

    private static string Normalize(string? thumbprint) =>
        string.IsNullOrWhiteSpace(thumbprint)
            ? string.Empty
            : thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static string Coalesce(string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
}
