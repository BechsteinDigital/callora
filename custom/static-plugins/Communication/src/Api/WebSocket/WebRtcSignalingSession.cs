using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk.WebRtc;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// The workspace context an accepted WebRTC signalling connection resolves to: the
/// <see cref="IWebRtcClient"/> that mints the server-side peer, the target
/// <see cref="WebRtcVoiceChannel"/> the connected call is attached to, and the call identity/target the
/// resulting <see cref="ICall"/> carries. Produced by <see cref="IWebRtcSignalingSessionResolver"/> from
/// the authorizer-resolved connection subject; the concrete provisioning is wired in a later slice (S4).
/// </summary>
/// <param name="Client">The WebRTC client used to create the session's peer connection.</param>
/// <param name="Channel">The channel the connected peer is tracked as an incoming call on.</param>
/// <param name="CallId">Correlates the resulting call across the signalling path.</param>
/// <param name="Target">The remote participant identity for the resulting call.</param>
public sealed record WebRtcSignalingSession(
    IWebRtcClient Client,
    WebRtcVoiceChannel Channel,
    string CallId,
    CallTarget Target);
