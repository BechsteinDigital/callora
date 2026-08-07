namespace Callora.Plugin.Composer.Domain;

/// <summary>
/// A layout's identity — which workspace, which surface, what it is called. Its content lives in
/// versions.
/// </summary>
public sealed class SurfaceLayout
{
    private SurfaceLayout()
    {
        Key = string.Empty;
        WorkspaceKey = string.Empty;
        Name = string.Empty;
    }

    public SurfaceLayout(string key, string workspaceKey, string? surfaceKey, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Key = key;
        WorkspaceKey = workspaceKey;
        SurfaceKey = surfaceKey;
        Name = name;
    }

    /// <summary>Stable identifier, unique per workspace.</summary>
    public string Key { get; private set; }

    public string WorkspaceKey { get; private set; }

    /// <summary>
    /// The surface this layout renders, or null for one that is not bound yet — a layout can be
    /// built before anyone decides where it goes.
    /// </summary>
    public string? SurfaceKey { get; private set; }

    public string Name { get; private set; }

    /// <summary>Binds the layout to a surface, or unbinds it.</summary>
    public void AssignSurface(string? surfaceKey) => SurfaceKey = surfaceKey;

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
