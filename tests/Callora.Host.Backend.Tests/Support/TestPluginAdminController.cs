using Callora.Host.PluginContracts.Application.Http;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>Admin-scope test controller for plugin routing tests.</summary>
public sealed class TestPluginAdminController : AdminApiController
{
    [CalloraRoute("GET", "/api/test-plugin/ping", Permission = "test.read")]
    public Task<ApiResult> PingAsync(ApiRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Ok(new { pong = true }));

    [CalloraRoute("POST", "/api/test-plugin/echo", Permission = "test.write")]
    public async Task<ApiResult> EchoAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        var body = await request.ReadJsonAsync<Dictionary<string, string>>(cancellationToken);
        return body is null || !body.TryGetValue("name", out var name)
            ? BadRequest("name is required.")
            : Created($"/api/test-plugin/echo/{name}", new { name });
    }
}
