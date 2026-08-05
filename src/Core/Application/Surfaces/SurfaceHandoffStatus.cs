namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Outcome of issuing or redeeming a handoff ticket (ADR-017 §8.4). Every failure is
/// distinct host-side so an operator can tell a misconfigured surface from a replayed
/// ticket; the visitor sees one uniform refusal.
/// </summary>
public enum SurfaceHandoffStatus
{
    /// <summary>The operation succeeded.</summary>
    Ok = 0,

    /// <summary>The request carried no authenticated surface caller to hand over.</summary>
    NotAuthenticated,

    /// <summary>The target surface does not exist, is inactive, or has no public host.</summary>
    TargetUnavailable,

    /// <summary>The ticket is unknown, already used, or expired.</summary>
    TicketInvalid,

    /// <summary>The ticket was presented on a host it was not minted for.</summary>
    AudienceMismatch,
}
