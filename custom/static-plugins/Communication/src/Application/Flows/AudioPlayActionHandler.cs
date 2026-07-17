using Callora.Core.Application.Media.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Audio;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Application.Channels;

namespace Callora.Plugin.Communication.Application.Flows;

/// <summary>
/// Plays an announcement from the media library ("mediaId") into the call —
/// the call must be connected (combine with a preceding call.accept).
/// </summary>
public sealed class AudioPlayActionHandler(
    VoipCallHub callHub,
    IMediaLibrary mediaLibrary) : VoipCallFlowActionHandlerBase(callHub)
{
    public override string Type => "audio.play";

    protected override async Task ExecuteOnCallAsync(
        ICall call,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("mediaId", out var rawMediaId) || !Guid.TryParse(rawMediaId, out var mediaId))
        {
            throw new InvalidOperationException("audio.play requires a 'mediaId' parameter.");
        }

        await using var content = await mediaLibrary.OpenReadAsync(mediaId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Media asset '{mediaId}' was not found.");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (call is not IVoipCall voipCall)
        {
            throw new InvalidOperationException("audio.play requires a voice-plugin call that exposes audio media.");
        }

        await using var audioStream = await voipCall.OpenAudioAsync(cancellationToken).ConfigureAwait(false);
        await AnnouncementStreamer.StreamAsync(audioStream, buffer.ToArray(), cancellationToken).ConfigureAwait(false);
    }
}
