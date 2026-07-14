namespace Callora.Host.Backend.Application.Plugins;

public sealed record TrustedPluginSigner(
    string PublisherId,
    string DisplayName,
    string Thumbprint,
    string Source);
