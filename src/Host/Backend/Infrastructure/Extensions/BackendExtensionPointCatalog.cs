using Callora.Host.Backend.Domain.Extensions;

namespace Callora.Host.Backend.Infrastructure.Extensions;

public static class BackendExtensionPointCatalog
{
    public static string Version => "1.0";

    public static IReadOnlyCollection<ExtensionPointDefinition> Build()
    {
        return
        [
            new ExtensionPointDefinition(
                ExtensionPointId: "workspace.navigation.main",
                Surface: ExtensionSurface.Workspace,
                RequiredScope: "workspace.navigation"),
            new ExtensionPointDefinition(
                ExtensionPointId: "workspace.theme.definition",
                Surface: ExtensionSurface.Workspace,
                RequiredScope: "workspace.theme.read"),
            new ExtensionPointDefinition(
                ExtensionPointId: "workspace.theme.settings",
                Surface: ExtensionSurface.Workspace,
                RequiredScope: "workspace.theme.write"),
            new ExtensionPointDefinition(
                ExtensionPointId: "admin.navigation.main",
                Surface: ExtensionSurface.Admin,
                RequiredScope: "admin.navigation"),
            new ExtensionPointDefinition(
                ExtensionPointId: "admin.api.route",
                Surface: ExtensionSurface.Admin,
                RequiredScope: "admin.api.route")
        ];
    }
}
