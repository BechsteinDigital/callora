namespace Callora.Core.Application.Surfaces.Events;

/// <summary>
/// Stable business-event names for surface caller lifecycle changes. Consumers
/// (flows, webhooks, plugin listeners) subscribe by these dotted names.
/// </summary>
public static class SurfaceCallerEventTypes
{
    /// <summary>
    /// A guest became an authenticated caller. Carries both subjects, because the
    /// subject changes at that moment and only the owning plugin can move the state
    /// that hung off the old one — cart, draft, progress. The host does not migrate
    /// anything: it does not know the data.
    /// </summary>
    public const string Promoted = "surface.caller-promoted";
}
