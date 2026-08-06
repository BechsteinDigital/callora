namespace Callora.Core.Application.Surfaces.Layout;

/// <summary>
/// What one block control is bound to. Mirrors the client-side <c>Binding&lt;T&gt;</c> — same four
/// kinds, because a layout authored in the editor and one read by the renderer are the same
/// document.
/// </summary>
/// <param name="Source">One of <c>static</c>, <c>context</c>, <c>inherit</c>, <c>default</c>.</param>
/// <param name="Value">The literal, for a static binding. Null otherwise.</param>
/// <param name="Key">The versioned context key, for a context binding. Null otherwise.</param>
/// <param name="Path">Optional path into the context value, e.g. <c>customer.name</c>.</param>
public sealed record SurfaceBlockBinding(
    string Source,
    object? Value = null,
    string? Key = null,
    string? Path = null)
{
    /// <summary>A literal the editor captured.</summary>
    public const string StaticSource = "static";

    /// <summary>A versioned context key, resolved in the browser and never on the server.</summary>
    public const string ContextSource = "context";

    /// <summary>Take it from the enclosing section.</summary>
    public const string InheritSource = "inherit";

    /// <summary>Whatever the block declares as its default.</summary>
    public const string DefaultSource = "default";
}
