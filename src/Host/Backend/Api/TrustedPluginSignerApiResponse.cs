namespace Callora.Host.Backend.Api;

public sealed record TrustedPluginSignerApiResponse(
    string PublisherId,
    string DisplayName,
    string Thumbprint,
    string Source);
