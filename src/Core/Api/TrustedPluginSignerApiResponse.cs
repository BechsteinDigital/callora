namespace Callora.Core.Api;

public sealed record TrustedPluginSignerApiResponse(
    string PublisherId,
    string DisplayName,
    string Thumbprint,
    string Source);
