namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// How many instances of a view a slot may hold (#125 block C). Workplace blocks are
/// not CMS blocks: a phone panel bound to a device or a live socket exists once, while
/// a note card can exist many times. Declaring which is which is what lets a later
/// layout editor refuse an impossible arrangement instead of producing one.
/// </summary>
public enum SurfaceViewCardinality
{
    /// <summary>Any number of instances may be placed in the slot.</summary>
    Multiple = 0,

    /// <summary>
    /// At most one instance (the epic's <c>single</c>; spelled out here because
    /// <c>Single</c> collides with a framework type name). A view declared this way is
    /// emitted once per slot no matter how often it is declared, so the outcome does
    /// not depend on plugin load order.
    /// </summary>
    AtMostOne = 1,
}
