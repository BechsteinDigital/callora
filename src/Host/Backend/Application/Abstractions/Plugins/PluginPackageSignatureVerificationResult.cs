namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public sealed record PluginPackageSignatureVerificationResult(
    bool IsValid,
    string? ErrorMessage = null,
    string? ErrorCode = null,
    string? SignerThumbprint = null);
