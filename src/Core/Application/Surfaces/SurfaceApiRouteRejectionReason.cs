namespace Callora.Core.Application.Surfaces;

/// <summary>Why a declared surface API route was not mounted (#125 block B).</summary>
public enum SurfaceApiRouteRejectionReason
{
    /// <summary>The plugin id is empty, contains a path separator, or is reserved by the platform.</summary>
    ReservedPluginId = 0,

    /// <summary>The route template is absolute or contains a traversal segment.</summary>
    InvalidTemplate,

    /// <summary>The same method and template are already mounted for this plugin.</summary>
    DuplicateRoute,
}
