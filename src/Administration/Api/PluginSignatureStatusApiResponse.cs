namespace Callora.Administration.Api;

/// <summary>
/// One plugin's signature standing (from re-verifying the installed assembly):
/// its state code and, if signed, the signer's key fingerprint.
/// </summary>
public sealed record PluginSignatureStatusApiResponse(
    string PluginId,
    string State,
    string? SignerFingerprint);
