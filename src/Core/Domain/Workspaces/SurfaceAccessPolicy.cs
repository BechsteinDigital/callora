namespace Callora.Core.Domain.Workspaces;

/// <summary>
/// Who may reach a workspace's public surface (ADR-014 §3.4). The default is
/// <see cref="Public"/> — the surface renders anonymously, as it always has.
/// <see cref="Authenticated"/> requires a logged-in user; the render route and the
/// workspace UI chain redirect/deny an anonymous caller. This is a workspace-level v1;
/// distinct per-surface policies follow once per-surface route resolution lands.
/// </summary>
public enum SurfaceAccessPolicy
{
    /// <summary>Anyone may reach the surface — no authentication required.</summary>
    Public = 0,

    /// <summary>Only an authenticated user may reach the surface.</summary>
    Authenticated = 1
}
