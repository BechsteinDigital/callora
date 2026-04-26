namespace Callora.Host.Backend.Application.Abstractions.Extensions;

public interface IWorkspaceTemplateResolutionCache
{
    void InvalidateWorkspace(string workspaceKey);

    void InvalidateTenant(string tenantKey);

    void InvalidateAll();
}
