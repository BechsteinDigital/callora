namespace Callora.Plugin.Communication.Application.Voice;

/// <summary>
/// Outcome of one reconciliation. The API result must reflect the runtime, not just the
/// write that preceded it (#110) — a handler reporting success while the account failed to
/// register is exactly the lie this type exists to prevent.
/// </summary>
/// <param name="State">What the runtime looks like now.</param>
/// <param name="Error">
/// Redacted, operator-facing reason when <see cref="State"/> is
/// <see cref="SipRuntimeState.Failed"/>; null otherwise.
/// </param>
public sealed record SipRuntimeReconciliation(SipRuntimeState State, string? Error = null)
{
    /// <summary>Whether the runtime now matches the account's desired state.</summary>
    public bool IsSuccess => State != SipRuntimeState.Failed;

    /// <summary>The account is connected and registered with its current configuration.</summary>
    public static SipRuntimeReconciliation Connected { get; } = new(SipRuntimeState.Connected);

    /// <summary>The account is not provisioned — disabled, deleted, or never connected.</summary>
    public static SipRuntimeReconciliation Removed { get; } = new(SipRuntimeState.Removed);

    /// <summary>The runtime could not reach the desired state; the account is not live.</summary>
    public static SipRuntimeReconciliation Failed(string error) =>
        new(SipRuntimeState.Failed, string.IsNullOrWhiteSpace(error) ? "The voice runtime rejected the account." : error);
}
