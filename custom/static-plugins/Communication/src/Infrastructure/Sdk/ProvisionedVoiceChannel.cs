using Callora.Plugin.Communication.Application.Voice;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// One account's live registration as the reconciler holds it: the registry handle, the
/// channel it wraps, and the configuration fingerprint it was connected under. The
/// fingerprint is what lets <see cref="SipAccountRuntimeReconciler"/> tell "already correct"
/// from "must reconnect" (#110).
/// </summary>
/// <param name="Registration">Registry handle; disposing it deregisters the channel.</param>
/// <param name="Channel">The audio-registering channel wrapping the provider's voice channel.</param>
/// <param name="Fingerprint">Runtime-relevant configuration the channel was connected under.</param>
internal sealed record ProvisionedVoiceChannel(
    IDisposable Registration,
    AudioRegisteringChannel Channel,
    string Fingerprint);
