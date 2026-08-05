namespace Callora.Plugin.Communication.Application.Admin.Streaming;

/// <summary>
/// Body of <c>POST calls/{callId}/media-streams</c>.
/// </summary>
/// <param name="ConsumerRef">
/// Who the stream is for, for example <c>ai-agent</c>. Recorded on the session so an operator can
/// tell one consumer's stream from another's.
/// </param>
/// <param name="Direction">
/// <c>inbound</c> (the consumer listens), <c>outbound</c> (the consumer speaks) or
/// <c>bidirectional</c>. Defaults to <c>bidirectional</c> when omitted.
/// </param>
public sealed record MintMediaStreamApiRequest(string? ConsumerRef, string? Direction);
