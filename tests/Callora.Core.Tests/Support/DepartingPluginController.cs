using Callora.Core.Application.Http.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Der Controller des Plugins, das mitten in einem Neubau der Routentabelle deaktiviert wird.
/// Eigene Route, damit sich am Ergebnis ablesen lässt, ob er noch bedient wird.
/// </summary>
public sealed class DepartingPluginController : AdminApiController
{
    public const string RoutePath = "/api/departing-plugin/ping";

    [CalloraRoute("GET", RoutePath, Permission = "test.read")]
    public Task<ApiResult> PingAsync(ApiRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Ok(new { pong = true }));
}
