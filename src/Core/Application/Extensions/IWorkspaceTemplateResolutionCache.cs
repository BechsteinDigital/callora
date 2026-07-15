namespace Callora.Core.Application.Extensions;

public interface IWorkspaceTemplateResolutionCache
{
    void InvalidateWorkspace(string workspaceKey);

    void InvalidateTenant(string tenantKey);

    void InvalidateAll();
}
