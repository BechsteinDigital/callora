namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Voice-spezifischer Call: der modalitätsneutrale <see cref="ICall"/> plus Zugriff
/// auf den Duplex-Audio-Stream (ADR-012/REV2 §10.1 C). In-process-Pfad für tief
/// integrierte .NET-Consumer; externe Consumer nutzen den WebSocket-Media-Stream.
/// </summary>
public interface IVoipCall : ICall
{
    /// <summary>Öffnet den bidirektionalen Audio-Stream des Calls.</summary>
    Task<ICallAudioStream> OpenAudioAsync(CancellationToken cancellationToken = default);
}
