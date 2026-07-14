namespace Callora.Host.Backend.Application.Events;

/// <summary>
/// Defines execution priority for host application event subscribers.
/// </summary>
public interface IHostApplicationEventSubscriberPriority
{
    /// <summary>
    /// Gets the subscriber priority. Higher values execute earlier.
    /// </summary>
    int Priority { get; }
}
