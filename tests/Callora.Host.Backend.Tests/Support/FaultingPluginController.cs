using Callora.Host.PluginContracts.Application.Http;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Test controller whose action throws, to prove the routing source turns a
/// plugin fault into a structured 500 instead of an unhandled exception
/// (audit finding M6).
/// </summary>
public sealed class FaultingPluginController : AdminApiController
{
    [CalloraRoute("GET", "/api/test-plugin/boom", Permission = "test.read")]
    public Task<ApiResult> BoomAsync(ApiRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("plugin action blew up");
}
