namespace Callora.Core.Domain.Extensions;

/// <summary>
/// Converts extension surfaces to and from stable API codes.
/// <para>
/// The tenant-facing surface is <c>surface</c>. It used to be <c>workspace</c>, which named the
/// wrong thing: a workspace is the container, a surface one of its access points, and a workspace
/// can expose several (ADR-014 §5). Persisted rows and manifests are migrated; the old code is
/// deliberately NOT accepted, so a bundle that still declares it fails visibly rather than
/// publishing nothing and leaving an operator to wonder.
/// </para>
/// </summary>
public static class ExtensionSurfaceCodes
{
    public const string Admin = "admin";
    public const string Surface = "surface";

    public static bool TryParse(string? value, out ExtensionSurface surface)
    {
        surface = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case Admin:
                surface = ExtensionSurface.Admin;
                return true;
            case Surface:
                surface = ExtensionSurface.Surface;
                return true;
            default:
                return false;
        }
    }

    public static string ToCode(this ExtensionSurface surface) =>
        surface switch
        {
            ExtensionSurface.Admin => Admin,
            ExtensionSurface.Surface => Surface,
            _ => "unknown"
        };
}
