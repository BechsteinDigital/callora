namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Who may reach a surface API route. The default is the safe one: a route is for
/// authenticated visitors unless it explicitly says otherwise (#125 block B).
/// </summary>
public enum SurfaceApiRouteAudience
{
    /// <summary>
    /// Only an authenticated surface caller. The default, and what the seam exists
    /// for: acting in the name of a real CRM, portal or clinic user.
    /// </summary>
    Authenticated = 0,

    /// <summary>
    /// A guest context is enough. An explicit opt-in for the state a recognised but
    /// unauthenticated visitor legitimately owns — a cart, a draft, a multi-step form
    /// (ADR-017 §3). The guest subject is a key, never an entitlement: a handler here
    /// must still refuse anything that needs an identity.
    /// </summary>
    GuestOrAuthenticated = 1,
}
