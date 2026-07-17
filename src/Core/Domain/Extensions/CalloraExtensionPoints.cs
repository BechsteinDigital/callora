namespace Callora.Core.Domain.Extensions;

/// <summary>
/// Canonical identifiers of the built-in extension points. The host catalogue and
/// plugins reference these constants instead of raw strings, so a mistyped point id
/// surfaces at compile time with IDE completion, rather than as a runtime activation
/// failure in the extension synchronizer. Names are prefixed by their surface.
/// </summary>
public static class CalloraExtensionPoints
{
    /// <summary>Workspace surface — main navigation menu.</summary>
    public const string WorkspaceNavigationMain = "workspace.navigation.main";

    /// <summary>Workspace surface — theme definition.</summary>
    public const string WorkspaceThemeDefinition = "workspace.theme.definition";

    /// <summary>Workspace surface — theme settings.</summary>
    public const string WorkspaceThemeSettings = "workspace.theme.settings";

    /// <summary>Admin surface — main navigation menu.</summary>
    public const string AdminNavigationMain = "admin.navigation.main";

    /// <summary>Admin surface — API route registration.</summary>
    public const string AdminApiRoute = "admin.api.route";
}
