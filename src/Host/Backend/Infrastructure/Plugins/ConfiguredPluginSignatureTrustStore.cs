using Callora.Host.Backend.Application.Abstractions.Plugins;
using Callora.Host.Backend.Application.Policies;

namespace Callora.Host.Backend.Infrastructure.Plugins;

public sealed class ConfiguredPluginSignatureTrustStore : IPluginSignatureTrustStore
{
    private readonly HashSet<string> _trustedThumbprints;
    private readonly TrustedPluginSigner[] _trustedSigners;

    public ConfiguredPluginSignatureTrustStore(BackendHostOptions options)
    {
        _trustedThumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trustedSigners = new List<TrustedPluginSigner>();

        foreach (var thumbprint in options.TrustedSignerThumbprints)
        {
            var normalized = Normalize(thumbprint);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                _trustedThumbprints.Add(normalized);
                trustedSigners.Add(new TrustedPluginSigner(
                    PublisherId: "legacy",
                    DisplayName: "Legacy Configured Signer",
                    Thumbprint: normalized,
                    Source: "backendHost.trustedSignerThumbprints"));
            }
        }

        foreach (var signer in options.TrustedSigners)
        {
            var normalized = Normalize(signer.Thumbprint);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            _trustedThumbprints.Add(normalized);
            trustedSigners.Add(new TrustedPluginSigner(
                PublisherId: string.IsNullOrWhiteSpace(signer.PublisherId) ? "unknown" : signer.PublisherId,
                DisplayName: string.IsNullOrWhiteSpace(signer.DisplayName) ? signer.PublisherId : signer.DisplayName,
                Thumbprint: normalized,
                Source: string.IsNullOrWhiteSpace(signer.Source) ? "backendHost.trustedSigners" : signer.Source));
        }

        _trustedSigners = trustedSigners
            .DistinctBy(static x => x.Thumbprint, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool IsTrusted(string? signerThumbprint)
    {
        var normalized = Normalize(signerThumbprint);
        return !string.IsNullOrWhiteSpace(normalized) && _trustedThumbprints.Contains(normalized);
    }

    public IReadOnlyList<TrustedPluginSigner> GetTrustedSigners() => _trustedSigners;

    private static string Normalize(string? thumbprint) =>
        string.IsNullOrWhiteSpace(thumbprint)
            ? string.Empty
            : thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
