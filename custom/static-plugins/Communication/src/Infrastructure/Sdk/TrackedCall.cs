using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// One call tracked by <see cref="SdkCallAudioRegistrar"/>: the call plus the exact state-changed
/// handler instance subscribed for it, so the registrar can unsubscribe deterministically on teardown.
/// </summary>
/// <param name="Call">The tracked voice call.</param>
/// <param name="Handler">The handler subscribed to <see cref="ICall.StateChanged"/> for this call.</param>
internal sealed record TrackedCall(IVoipCall Call, EventHandler<CallStateChangedEventArgs> Handler);
