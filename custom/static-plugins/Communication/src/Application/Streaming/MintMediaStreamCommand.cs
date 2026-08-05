using Callora.Plugin.Communication.Domain.Streaming;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Request to attach a consumer's media socket to a live call.
/// </summary>
/// <param name="WorkspaceKey">Workspace the caller is authorized for; the call must belong to it.</param>
/// <param name="CallId">The live call to stream.</param>
/// <param name="ConsumerRef">
/// Who the stream is for (for example <c>ai-agent</c>). Recorded on the session so an operator can
/// tell one consumer's stream from another's.
/// </param>
/// <param name="Direction">Audio flow the consumer is allowed to use, relative to itself.</param>
public readonly record struct MintMediaStreamCommand(
    string WorkspaceKey,
    string CallId,
    string ConsumerRef,
    MediaStreamDirection Direction);
