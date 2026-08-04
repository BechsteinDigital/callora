using Callora.Core.Application.Extensions;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Cache stand-in for endpoint tests that only need the invalidation calls to
/// succeed — the resolution cache itself is covered by its own tests.
/// </summary>
public sealed class NoOpWorkspaceTemplateResolutionCache : IWorkspaceTemplateResolutionCache
{
    public void InvalidateWorkspace(string workspaceKey)
    {
    }

    public void InvalidateTenant(string tenantKey)
    {
    }

    public void InvalidateAll()
    {
    }
}
