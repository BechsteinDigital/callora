namespace Callora.Core.Application.Surfaces;

/// <summary>
/// How identity resolution ended for one surface request (ADR-017 §6). The split
/// that matters is between "nobody was recognised" and "the surface cannot answer
/// the question right now": the first continues as a guest wherever the access mode
/// allows it, the second closes the surface for authenticated access rather than
/// falling back to anonymous — a missing identity provider would otherwise be an
/// access leak, not a cosmetic defect.
/// </summary>
public enum SurfaceIdentityResolutionStatus
{
    /// <summary>Nobody was recognised. A guest continues where the access mode allows.</summary>
    Anonymous = 0,

    /// <summary>An identity was established and normalised.</summary>
    Authenticated = 1,

    /// <summary>
    /// No provider is assigned and the host could not derive one either. Only closes
    /// a surface whose access mode demands an identity.
    /// </summary>
    ProviderNotAssigned = 2,

    /// <summary>
    /// The assigned plugin is not effectively available in this workspace —
    /// deactivated, unentitled, unhealthy or removed.
    /// </summary>
    ProviderUnavailable = 3,

    /// <summary>The assigned plugin is available but exports no identity provider.</summary>
    ProviderMissing = 4,

    /// <summary>The provider timed out, threw, or returned a candidate the host refused.</summary>
    ProviderFailed = 5,
}
