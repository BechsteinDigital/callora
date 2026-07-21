namespace Callora.Plugin.Communication.Application.Persistence;

/// <summary>
/// One configured SIP account, stored as a real typed entity in the plugin
/// database (PLAT-260) — replaces the former jsonb document. The secret is
/// stored encrypted at rest (host data protector); the composite key is
/// workspace + account id.
/// </summary>
public sealed class SipAccount
{
    public string WorkspaceKey { get; set; } = string.Empty;

    public string SipAccountId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Domain { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Encrypted secret at rest.</summary>
    public string ProtectedSecret { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
