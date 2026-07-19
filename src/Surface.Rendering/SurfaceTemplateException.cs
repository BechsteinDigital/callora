namespace Callora.Surface.Rendering;

/// <summary>
/// Raised when a surface template fails to parse, hits a sandbox limit
/// (loop/recursion), errors at render time, or produces oversized output.
/// </summary>
public sealed class SurfaceTemplateException : Exception
{
    public SurfaceTemplateException()
    {
    }

    public SurfaceTemplateException(string message)
        : base(message)
    {
    }

    public SurfaceTemplateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
