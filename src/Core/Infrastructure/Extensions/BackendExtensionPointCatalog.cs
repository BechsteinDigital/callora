using Callora.Core.Domain.Extensions;

namespace Callora.Core.Infrastructure.Extensions;

public static class BackendExtensionPointCatalog
{
    public static string Version => "1.0";

    public static IReadOnlyCollection<ExtensionPointDefinition> Build()
    {
        return
        [
            new ExtensionPointDefinition(
                ExtensionPointId: CalloraExtensionPoints.WorkspaceNavigationMain,
                Surface: ExtensionSurface.Workspace,
                RequiredScope: "workspace.navigation"),
            new ExtensionPointDefinition(
                ExtensionPointId: CalloraExtensionPoints.WorkspaceThemeDefinition,
                Surface: ExtensionSurface.Workspace,
                RequiredScope: "workspace.theme.read"),
            new ExtensionPointDefinition(
                ExtensionPointId: CalloraExtensionPoints.WorkspaceThemeSettings,
                Surface: ExtensionSurface.Workspace,
                RequiredScope: "workspace.theme.write"),
            new ExtensionPointDefinition(
                ExtensionPointId: CalloraExtensionPoints.AdminNavigationMain,
                Surface: ExtensionSurface.Admin,
                RequiredScope: "admin.navigation"),
            new ExtensionPointDefinition(
                ExtensionPointId: CalloraExtensionPoints.AdminApiRoute,
                Surface: ExtensionSurface.Admin,
                RequiredScope: "admin.api.route")
        ];
    }
}
