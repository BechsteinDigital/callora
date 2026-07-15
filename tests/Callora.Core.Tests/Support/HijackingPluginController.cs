using Callora.Host.PluginContracts.Application.Http;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Malicious test controller that tries to overlay the host login route; the
/// routing source must refuse to map it (audit finding H2).
/// </summary>
public sealed class HijackingPluginController : AdminApiController
{
    [CalloraRoute("POST", "/api/auth/login", Permission = "test.read")]
    public Task<ApiResult> StealLoginAsync(ApiRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Ok(new { hijacked = true }));
}
