using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Domain.Lines;

/// <summary>
/// A callable identity (AOR/number) under a <see cref="Accounts.SipAccount"/>. Inbound routing
/// targets a line; outbound calls originate from one. Its status is not stored but
/// <em>derived</em> from the owning account's connectivity, the line's enabled flag and its
/// current call occupancy (<see cref="ResolveStatus"/>).
/// </summary>
public sealed class SipLine
{
    /// <summary>Creates a line under an account.</summary>
    public SipLine(
        string id,
        string accountId,
        string workspaceKey,
        string label,
        string sipUri,
        string? primaryNumber,
        bool enabled,
        string? inboundRoutingTarget)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(sipUri);

        Id = id;
        AccountId = accountId;
        WorkspaceKey = workspaceKey;
        Label = label;
        SipUri = sipUri;
        PrimaryNumber = primaryNumber;
        Enabled = enabled;
        InboundRoutingTarget = inboundRoutingTarget;
    }

    /// <summary>Stable line identifier.</summary>
    public string Id { get; }

    /// <summary>Owning account identifier.</summary>
    public string AccountId { get; }

    /// <summary>Owning workspace.</summary>
    public string WorkspaceKey { get; }

    /// <summary>Operator-facing label.</summary>
    public string Label { get; private set; }

    /// <summary>The SIP identity (AOR) of this line.</summary>
    public string SipUri { get; private set; }

    /// <summary>Optional primary number (DID) bound to this line.</summary>
    public string? PrimaryNumber { get; private set; }

    /// <summary>Whether the line is active.</summary>
    public bool Enabled { get; private set; }

    /// <summary>How inbound calls on this line are dispatched (flow id / consumer capability).</summary>
    public string? InboundRoutingTarget { get; private set; }

    /// <summary>
    /// Derives the operational availability from the owning account's connectivity and whether
    /// the line currently carries a call.
    /// </summary>
    public SipLineStatus ResolveStatus(SipAccountStatus accountStatus, bool isBusy)
    {
        if (!Enabled)
        {
            return SipLineStatus.Disabled;
        }

        if (accountStatus != SipAccountStatus.Up)
        {
            return SipLineStatus.Unavailable;
        }

        return isBusy ? SipLineStatus.Busy : SipLineStatus.Available;
    }
}
