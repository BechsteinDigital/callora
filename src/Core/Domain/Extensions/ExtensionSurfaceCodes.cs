namespace Callora.Core.Domain.Extensions;

/// <summary>
/// Converts extension surfaces to and from stable API codes.
/// </summary>
public static class ExtensionSurfaceCodes
{
    public const string Admin = "admin";
    public const string Workspace = "workspace";

    public static bool TryParse(string? value, out ExtensionSurface surface)
    {
        surface = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        surface = value.Trim().ToLowerInvariant() switch
        {
            Admin => ExtensionSurface.Admin,
            Workspace => ExtensionSurface.Workspace,
            _ => default
        };

        return value.Trim().Equals(Admin, StringComparison.OrdinalIgnoreCase) ||
               value.Trim().Equals(Workspace, StringComparison.OrdinalIgnoreCase);
    }

    public static string ToCode(this ExtensionSurface surface) =>
        surface switch
        {
            ExtensionSurface.Admin => Admin,
            ExtensionSurface.Workspace => Workspace,
            _ => "unknown"
        };
}
