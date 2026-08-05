namespace Callora.Core.Domain.Plugins;

/// <summary>
/// A plugin's promise that a real-time session can be picked back up (ADR-018 §2.2). Stored rather
/// than held in memory for one reason: the drop worth surviving is the one that takes the process
/// with it.
/// </summary>
/// <remarks>
/// The row keeps only the hash of the secret, so a leaked database yields no redeemable ticket, and
/// the short lifetime plus single use bound the damage even if the secret itself is intercepted.
/// The payload is the plugin's own and opaque here.
/// </remarks>
public sealed class SessionResumeTicketRecord
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 of the ticket secret, hex encoded. The secret itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Plugin that issued the ticket. Only it may redeem it.</summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>The plugin's own name for this kind of session.</summary>
    public string SessionKind { get; set; } = string.Empty;

    /// <summary>Workspace the session belonged to, empty when it had none.</summary>
    public string WorkspaceKey { get; set; } = string.Empty;

    /// <summary>What the plugin needs to rebuild the session. Never interpreted here.</summary>
    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset IssuedAtUtc { get; set; }

    /// <summary>When the promise lapses. Deliberately short.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
