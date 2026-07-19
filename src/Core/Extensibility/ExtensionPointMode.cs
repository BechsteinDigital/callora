namespace Callora.Core.Extensibility;

/// <summary>
/// How a plugin may extend a Callora extension point marked with
/// <see cref="CalloraExtensibleAttribute"/> (REV2 §4.1). The mode is part of the
/// contract: it states what a plugin is allowed to do with the marked surface.
/// Surfaces whose final outcome must stay in the host carry no extension marker and
/// are guarded by <see cref="HostProtectedAttribute"/> instead.
/// </summary>
public enum ExtensionPointMode
{
    /// <summary>
    /// A plugin adds a further implementation alongside the host's — additive, no
    /// replacement (e.g. event listeners, navigation entries, export sections).
    /// This is the default for a marked contract.
    /// </summary>
    Contributable,

    /// <summary>
    /// A plugin wraps the host service to change or extend its behavior through an
    /// exported <c>IServiceDecorator&lt;TService&gt;</c>, delegating to the inner
    /// service for unchanged paths (e.g. mail, media, business policies).
    /// </summary>
    Decoratable,

    /// <summary>
    /// A plugin replaces the host implementation entirely under deterministic
    /// precedence — only for non-critical resolvers or business handlers.
    /// </summary>
    Replaceable,
}
