namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Bidirektionaler Audio-Stream eines Voice-Calls (in-process, sekundäre Fläche).
/// Inbound über <see cref="FrameReceived"/>, outbound über <see cref="SendAsync"/> —
/// der präzise Sende-Takt (monotone Clock, kein <c>Task.Delay</c>) liegt beim Consumer.
/// Externe Consumer nutzen stattdessen den WebSocket-Media-Stream der Foundation.
/// </summary>
public interface ICallAudioStream : IAsyncDisposable
{
    /// <summary>Format der Frames in beiden Richtungen.</summary>
    AudioFormat Format { get; }

    /// <summary>
    /// Eingehende Frames. Handler dürfen nicht blockieren. Der Frame-Speicher ist nur während des
    /// Callbacks gültig — für spätere Nutzung im Callback kopieren (Ownership-Vertrag, siehe
    /// <see cref="AudioFrameReceivedEventArgs"/>).
    /// </summary>
    event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

    /// <summary>
    /// Sendet einen ausgehenden Frame an die Gegenstelle. Die Implementierung darf
    /// <paramref name="frame"/> NICHT über die Fertigstellung des zurückgegebenen
    /// <see cref="ValueTask"/> hinaus behalten (bei Bedarf im Aufruf kopieren); der Aufrufer darf
    /// den Puffer danach wiederverwenden.
    /// </summary>
    ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default);
}
