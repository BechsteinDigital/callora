using Callora.Contracts.Communication;
using Callora.Host.PluginContracts.Application.Media;
using Callora.Plugins.Voip.Application.Audio;
using Callora.Plugins.Voip.Application.Calls;

namespace Callora.Plugins.Voip.Application.Flows;

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

        await using var audioStream = await call.OpenAudioAsync(cancellationToken).ConfigureAwait(false);
        await AnnouncementStreamer.StreamAsync(audioStream, buffer.ToArray(), cancellationToken).ConfigureAwait(false);
    }
}
