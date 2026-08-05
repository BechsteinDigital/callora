namespace Callora.Plugin.Communication.Application.Admin;

/// <summary>
/// One dependency in the Communication readiness answer (#112).
/// </summary>
/// <param name="Name">
/// Stable identifier of the dependency (<c>database</c>, <c>channels</c>, <c>sip</c>,
/// <c>webrtc</c>). Stable so a monitor can alert on a specific one.
/// </param>
/// <param name="State">
/// <c>up</c>, <c>degraded</c>, <c>down</c>, or <c>not-configured</c> for a dependency this
/// deployment deliberately does not use. A dependency that is not configured never drags the
/// aggregate down: a voice-only install is ready without WebRTC.
/// </param>
/// <param name="Detail">
/// Short, operator-facing explanation. Redacted the same way account errors are, because it
/// can quote a provider message.
/// </param>
public sealed record CommunicationDependencyStatus(string Name, string State, string? Detail);
