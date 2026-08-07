using Callora.Plugin.Communication.Application.Voice;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// One account's live registration as the reconciler holds it: the registry handle, the
/// channel it wraps, the configuration fingerprint it was connected under, and the handler
/// that mirrors the channel's health onto the account.
/// <para>
/// The fingerprint is what lets <see cref="SipAccountRuntimeReconciler"/> tell "already
/// correct" from "must reconnect" (#110). The subscription is disposed with the channel, or a
/// torn-down channel would keep writing status for an account the reconciler no longer
/// owns (#112).
/// </para>
/// </summary>
/// <param name="WorkspaceKey">
/// The workspace the channel is registered under. Kept here because teardown works from the tracking
/// key, and a workspace key may itself contain the separator that key is built from.
/// </param>
/// <param name="Registration">Registry handle; disposing it deregisters the channel.</param>
/// <param name="Channel">The audio-registering channel wrapping the provider's voice channel.</param>
/// <param name="Fingerprint">Runtime-relevant configuration the channel was connected under.</param>
/// <param name="HealthSubscription">
/// Detaches the health handler. Null when the deployment has no status projector.
/// </param>
internal sealed record ProvisionedVoiceChannel(
    string WorkspaceKey,
    IDisposable Registration,
    AudioRegisteringChannel Channel,
    string Fingerprint,
    IDisposable? HealthSubscription);
