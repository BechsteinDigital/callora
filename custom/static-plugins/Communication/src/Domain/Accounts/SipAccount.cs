using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// A configured connection to a SIP registrar or trunk. Owns the connectivity status
/// (driven by the SDK bridge); credentials live in the secret store via the connection's
/// password reference, never in the entity.
/// </summary>
public sealed class SipAccount
{
    /// <summary>Creates a configured account. Status starts <see cref="SipAccountStatus.Connecting"/>
    /// when enabled, otherwise <see cref="SipAccountStatus.Disabled"/>.</summary>
    public SipAccount(
        string id,
        string workspaceKey,
        string displayName,
        SipConnection connection,
        int maxConcurrentCalls,
        bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentCalls, 1);

        Id = id;
        WorkspaceKey = workspaceKey;
        DisplayName = displayName;
        Connection = connection;
        MaxConcurrentCalls = maxConcurrentCalls;
        Enabled = enabled;
        Status = enabled ? SipAccountStatus.Connecting : SipAccountStatus.Disabled;
    }

#pragma warning disable CS8618 // Materialisierungs-Seam: EF setzt die Properties (inkl. des OwnsOne-VO) nach dem Aufruf.
    private SipAccount()
    {
    }
#pragma warning restore CS8618

    /// <summary>Stable account identifier.</summary>
    public string Id { get; }

    /// <summary>Owning workspace.</summary>
    public string WorkspaceKey { get; }

    /// <summary>Operator-facing display name.</summary>
    public string DisplayName { get; private set; }

    /// <summary>Connection configuration.</summary>
    public SipConnection Connection { get; private set; }

    /// <summary>Maximum simultaneous calls across this account's lines.</summary>
    public int MaxConcurrentCalls { get; private set; }

    /// <summary>Whether the account is active.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Current connectivity status (registration or trunk reachability).</summary>
    public SipAccountStatus Status { get; private set; }

    /// <summary>Last error reported with a <see cref="SipAccountStatus.Failed"/> transition.</summary>
    public string? LastError { get; private set; }

    /// <summary>When the status last changed.</summary>
    public DateTimeOffset? LastStatusChangeAt { get; private set; }

    /// <summary>Records a connectivity-status transition reported by the SDK bridge.</summary>
    public void ReportStatus(SipAccountStatus status, string? error, DateTimeOffset at)
    {
        Status = status;
        LastError = status == SipAccountStatus.Failed ? error : null;
        LastStatusChangeAt = at;
    }

    /// <summary>Enables the account so it is provisioned into a live channel. Idempotent; when it was
    /// disabled the status re-enters <see cref="SipAccountStatus.Connecting"/> until the bridge reports.</summary>
    public void Enable()
    {
        Enabled = true;
        if (Status == SipAccountStatus.Disabled)
        {
            Status = SipAccountStatus.Connecting;
        }
    }

    /// <summary>Disables the account so it is not provisioned. Idempotent; status becomes
    /// <see cref="SipAccountStatus.Disabled"/> and any last error is cleared.</summary>
    public void Disable()
    {
        Enabled = false;
        Status = SipAccountStatus.Disabled;
        LastError = null;
    }

    /// <summary>
    /// Replaces the account's editable configuration (operator update). Identity, workspace and the
    /// enabled/status lifecycle are unaffected — enabling is done via <see cref="Enable"/>/<see cref="Disable"/>,
    /// and connectivity status is reported by the bridge. Re-provisioning after a change is a runtime concern.
    /// </summary>
    public void Reconfigure(string displayName, SipConnection connection, int maxConcurrentCalls)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentCalls, 1);

        DisplayName = displayName;
        Connection = connection;
        MaxConcurrentCalls = maxConcurrentCalls;
    }
}
