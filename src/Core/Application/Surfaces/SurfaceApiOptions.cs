namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Host bounds on a plugin surface API request (#125 block B). They exist because the
/// seam is reachable by anyone holding a surface context, which on a public surface is
/// anyone at all.
/// </summary>
public sealed class SurfaceApiOptions
{
    /// <summary>Configuration section binding these options.</summary>
    public const string SectionName = "Callora:SurfaceApi";

    /// <summary>
    /// Hard cap on the request body. Enforced before the handler is reached and
    /// regardless of a declared content length, since a chunked request declares none.
    /// </summary>
    public int MaxRequestBodyBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// How long a handler may run before its cancellation token fires. A plugin that
    /// hangs must not hold a surface request open indefinitely.
    /// </summary>
    public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
