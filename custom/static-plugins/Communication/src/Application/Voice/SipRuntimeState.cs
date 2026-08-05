namespace Callora.Plugin.Communication.Application.Voice;

/// <summary>What the voice runtime holds for an account after a reconciliation.</summary>
public enum SipRuntimeState
{
    /// <summary>Connected and registered with the account's current configuration.</summary>
    Connected = 0,

    /// <summary>Not provisioned — disabled, deleted, or never connected.</summary>
    Removed = 1,

    /// <summary>The desired state could not be reached; the account is not live.</summary>
    Failed = 2
}
