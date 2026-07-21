namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Ein Channel mit Voice-Fähigkeit: eingehende und ausgehende Calls sind
/// <see cref="IVoipCall"/> mit Audio-Zugriff.
/// </summary>
public interface IVoiceChannel : ICommunicationChannel
{
}
