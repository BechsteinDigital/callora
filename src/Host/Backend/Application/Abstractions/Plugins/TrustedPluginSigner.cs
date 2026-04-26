namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public sealed record TrustedPluginSigner(
    string PublisherId,
    string DisplayName,
    string Thumbprint,
    string Source);
