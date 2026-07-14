namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Well-known capability codes used in channel declarations and plugin
/// registry manifests (requiresCapabilities/capabilities).
/// </summary>
public static class CommunicationCapabilities
{
    /// <summary>Outbound and inbound voice calls.</summary>
    public const string Voice = "communication.voice";
}
