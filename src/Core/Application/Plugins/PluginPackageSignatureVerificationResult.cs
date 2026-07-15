namespace Callora.Core.Application.Plugins;

public sealed record PluginPackageSignatureVerificationResult(
    bool IsValid,
    string? ErrorMessage = null,
    string? ErrorCode = null,
    string? SignerThumbprint = null);
