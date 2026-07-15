using Callora.Core.Application.Http.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>Workspace-scope test controller for plugin routing tests.</summary>
public sealed class TestPluginWorkspaceController : WorkspaceApiController
{
    [CalloraRoute("GET", "/api/test-plugin/items", Permission = "test.read")]
    public Task<ApiResult> ListAsync(ApiRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Ok(new { workspaceKey = request.WorkspaceKey }));
}
