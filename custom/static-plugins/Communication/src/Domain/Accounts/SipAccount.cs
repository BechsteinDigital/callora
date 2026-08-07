using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Domain.Accounts;

/// <summary>
/// A configured connection to a SIP registrar or trunk. Owns the connectivity status
/// (driven by the SDK bridge); credentials live in the secret store via the connection's
/// password reference, never in the entity.
/// </summary>
public sealed class SipAccount
{
    private IReadOnlyList<CallQuota>? _callQuotas;

    /// <summary>Creates a configured account. Status starts <see cref="SipAccountStatus.Connecting"/>
    /// when enabled, otherwise <see cref="SipAccountStatus.Disabled"/>.</summary>
    public SipAccount(
        string id,
        string workspaceKey,
        string displayName,
        SipConnection connection,
        int maxConcurrentCalls,
        bool enabled,
        IEnumerable<CallQuota>? callQuotas = null)
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
        CallQuotas = CallQuota.Validate(callQuotas);
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

    /// <summary>
    /// How the account's lines are divided between the things that use it. Empty means undivided:
    /// splitting a trunk is deliberate, and an operator who configured nothing wanted no split — not a
    /// silent limit of zero.
    /// </summary>
    /// <remarks>
    /// Reads as empty even when nothing was materialized: a NULL column bypasses the value converter,
    /// so EF hands the field null rather than the empty list the converter would have produced. Every
    /// account that predates this column is in exactly that state.
    /// </remarks>
    public IReadOnlyList<CallQuota> CallQuotas
    {
        get => _callQuotas ?? [];
        private set => _callQuotas = value;
    }

    /// <summary>Whether the account is active.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Current connectivity status (registration or trunk reachability).</summary>
    public SipAccountStatus Status { get; private set; }

    /// <summary>Last error reported with a <see cref="SipAccountStatus.Failed"/> transition.</summary>
    public string? LastError { get; private set; }

    /// <summary>When the status last changed.</summary>
    public DateTimeOffset? LastStatusChangeAt { get; private set; }

    /// <summary>
    /// When the account last reached <see cref="SipAccountStatus.Up"/>. Survives later
    /// failures, so an operator can tell "never worked" from "worked until an hour ago" (#112).
    /// </summary>
    public DateTimeOffset? LastRegisteredAt { get; private set; }

    /// <summary>
    /// Records a connectivity-status transition reported by the provider bridge. Repeating the
    /// current status is a no-op, so a flapping-but-unchanged registration does not rewrite the
    /// transition timestamp on every heartbeat.
    /// </summary>
    /// <param name="status">The status the provider now reports.</param>
    /// <param name="error">
    /// Reason for a <see cref="SipAccountStatus.Failed"/> or <see cref="SipAccountStatus.Degraded"/>
    /// transition. Redacted through <see cref="SipStatusError"/> before it is kept, because a
    /// provider message can carry the credential that caused the failure.
    /// </param>
    /// <param name="at">When the provider observed the transition.</param>
    public void ReportStatus(SipAccountStatus status, string? error, DateTimeOffset at)
    {
        var redacted = status is SipAccountStatus.Failed or SipAccountStatus.Degraded
            ? SipStatusError.Redact(error)
            : null;

        if (Status == status && LastError == redacted)
        {
            return;
        }

        Status = status;
        LastError = redacted;
        LastStatusChangeAt = at;

        if (status == SipAccountStatus.Up)
        {
            LastRegisteredAt = at;
        }
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
    /// <param name="displayName">Operator-facing name.</param>
    /// <param name="connection">The connection configuration.</param>
    /// <param name="maxConcurrentCalls">The account's ceiling.</param>
    /// <param name="callQuotas">
    /// How the lines are divided. An empty set removes the division — there has to be a way back to an
    /// undivided trunk, so this replaces rather than merges. A caller that means "leave it alone"
    /// passes the account's current quotas.
    /// </param>
    public void Reconfigure(
        string displayName,
        SipConnection connection,
        int maxConcurrentCalls,
        IEnumerable<CallQuota>? callQuotas = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentCalls, 1);

        DisplayName = displayName;
        Connection = connection;
        MaxConcurrentCalls = maxConcurrentCalls;
        CallQuotas = CallQuota.Validate(callQuotas);
    }
}
