namespace Callora.Core.Infrastructure.Events;

/// <summary>
/// Ein aufzurufender Abonnent samt Priorität und — falls er aus einem Plugin stammt — dessen Id.
/// </summary>
/// <param name="Priority">Reihenfolge; höher läuft früher.</param>
/// <param name="Callback">Der Aufruf selbst.</param>
/// <param name="OwnerPluginId">
/// Das besitzende Plugin, oder <c>null</c> für einen Host-Abonnenten. Ohne diese Angabe war ein
/// geworfener Abonnent nicht zurechenbar: Der Dispatcher fängt jeden Fehler und macht weiter,
/// damit ein Abonnent die übrigen nicht mitreißt — richtig, aber es machte einen dauerhaft
/// scheiternden Plugin-Abonnenten unsichtbar. Er stand danach nur als Warnung im Log, ohne dass
/// irgendetwas ihn zählte.
/// </param>
internal sealed record DispatchHandler<TEvent>(
    int Priority,
    Func<TEvent, CancellationToken, Task> Callback,
    string? OwnerPluginId = null);
