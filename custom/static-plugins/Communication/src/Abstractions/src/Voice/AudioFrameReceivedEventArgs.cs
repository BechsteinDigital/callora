namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Ein eingehender Audio-Frame (inbound). Handler dürfen NICHT blockieren — den
/// Frame in eine Queue schreiben und sofort zurückkehren.
/// </summary>
/// <remarks>
/// <b>Ownership-Vertrag:</b> Der Speicher hinter <see cref="Frame"/> ist NUR für die Dauer des
/// Callbacks gültig. Ein Handler, der den Frame über die Rückkehr hinaus behält (z.B. in eine
/// Queue schreibt), MUSS ihn im Callback KOPIEREN (etwa <c>frame.ToArray()</c>) — der Producer
/// darf den zugrunde liegenden Puffer nach der Rückkehr wiederverwenden oder überschreiben
/// (gepoolte RTP-Buffer). <see cref="ReadOnlyMemory{T}"/> direkt zu behalten führt sonst zu
/// veränderten Daten in der Queue.
/// </remarks>
/// <param name="frame">Der rohe kodierte Frame gemäß <see cref="ICallAudioStream.Format"/>.</param>
public sealed class AudioFrameReceivedEventArgs(ReadOnlyMemory<byte> frame) : EventArgs
{
    /// <summary>
    /// Der rohe kodierte Frame gemäß <see cref="ICallAudioStream.Format"/>. Nur während des
    /// Callbacks gültig — für spätere Nutzung im Callback kopieren (siehe Ownership-Vertrag).
    /// </summary>
    public ReadOnlyMemory<byte> Frame { get; } = frame;
}
