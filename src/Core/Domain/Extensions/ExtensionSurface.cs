namespace Callora.Core.Domain.Extensions;

/// <summary>
/// Identifies the API surface where an extension point is exposed.
/// <para>
/// <see cref="Surface"/> was called <c>Workspace</c> and named the wrong thing: a workspace is
/// the container, a surface is one of its access points, and a workspace can expose several
/// (ADR-014 §5). The numeric values are unchanged, so persisted rows keep their meaning.
/// </para>
/// </summary>
public enum ExtensionSurface
{
    Admin = 0,
    Surface = 1
}
