using Callora.Core.Application.Surfaces;
using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Wer einen Surface-Knoten sehen darf (ADR-019 §4).
/// <para>
/// <b>Nicht das Operator-RBAC.</b> <c>BackendRbacRole</c> regelt, wer im Admin was darf; ein
/// Portal-Besucher ist kein Operator und hat keine Backend-Rolle. Geprüft werden die Claims des
/// <see cref="SurfaceCaller"/> aus ADR-017 — dieselbe Identität, die das Rendering ohnehin
/// trägt, und dasselbe Muster, mit dem <c>SurfaceSlotResolver</c> schon Views filtert.
/// </para>
/// </summary>
public static class SurfaceVisibility
{
    /// <summary>
    /// Ob dieser Knoten für diesen Besucher existiert — geprüft über die ganze Kette.
    /// <para>
    /// <b>Anforderungen sind kumulativ und nicht überschreibbar.</b> Verlangt <c>/portal/partner</c>
    /// einen Claim, so gilt er auch für <c>/portal/partner/downloads</c> — auch wenn der Knoten
    /// selbst nichts verlangt. Anders wäre der Schutz durch Tieferklicken zu umgehen: Die
    /// Unterseite hat eine eigene URL, die sich direkt aufrufen lässt, ohne je an der
    /// geschützten vorbeizukommen.
    /// </para>
    /// <para>
    /// Das ist bewusst anders als der Access Mode, der in beide Richtungen überschreibbar ist
    /// (§3.1). Der Modus sagt „muss angemeldet sein" — eine Eigenschaft des Zugangs, die eine
    /// öffentliche Unterseite unter einem geschlossenen Portal erlauben soll. Ein Claim sagt
    /// „darf das sehen", und was einmal verlangt wurde, darf eine Ebene tiefer nicht wegfallen.
    /// </para>
    /// </summary>
    /// <param name="ancestry">Die Kette vom Knoten zur Wurzel.</param>
    /// <param name="caller">Wer gerade fragt.</param>
    public static bool IsVisibleTo(IReadOnlyList<WorkspaceSurface> ancestry, SurfaceCaller caller)
    {
        ArgumentNullException.ThrowIfNull(ancestry);
        ArgumentNullException.ThrowIfNull(caller);

        var claims = ClaimsOf(caller);
        return ancestry.All(node => Satisfies(node.RequiredClaims, claims));
    }

    /// <summary>
    /// Ob diese Claim-Anforderung erfüllt ist. Leer heißt: keine Anforderung.
    /// <para>
    /// UND, nicht ODER — alle geforderten Claims müssen da sein. Das ist die vorsichtige
    /// Richtung und dieselbe, die <c>SurfaceSlotResolver</c> für Views anwendet: Wer zwei Claims
    /// fordert, meint beide, und ein ODER machte aus zwei Bedingungen versehentlich eine.
    /// </para>
    /// </summary>
    public static bool Satisfies(string? requiredClaims, IReadOnlySet<string> callerClaims)
    {
        ArgumentNullException.ThrowIfNull(callerClaims);

        return Parse(requiredClaims).All(callerClaims.Contains);
    }

    /// <summary>
    /// Die geforderten Claims eines Knotens. Gespeichert als kommagetrennte Liste, weil es
    /// selten mehr als zwei sind und nichts über sie hinweg abfragt.
    /// </summary>
    public static IReadOnlyList<string> Parse(string? requiredClaims) =>
        string.IsNullOrWhiteSpace(requiredClaims)
            ? []
            : requiredClaims
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    /// <summary>
    /// Die Claims des Besuchers. Ein Gast hat keine — auch nicht implizit: Ein anonymer Aufruf
    /// darf nie eine Anforderung erfüllen, die jemand ausdrücklich gestellt hat.
    /// </summary>
    public static IReadOnlySet<string> ClaimsOf(SurfaceCaller caller) =>
        caller is AuthenticatedSurfaceCaller authenticated
            ? authenticated.Identity.Claims.Keys.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Was dieser Besucher auf dieser Fläche mitbringt: seine eigenen Claims und die, die die
    /// Fläche jedem gewährt.
    /// </summary>
    /// <remarks>
    /// Ohne den zweiten Teil hatte ein nicht angemeldeter Besucher IMMER eine leere Menge. Jede
    /// Ansicht mit einer Anforderung war damit auf einer Fläche ohne Identitätsanbieter
    /// unerreichbar — auch für einen Gast mit gültiger Einladung, und ohne dass irgendwo stand,
    /// warum.
    /// </remarks>
    public static IReadOnlySet<string> ClaimsOn(SurfaceCaller caller, string? grantedClaims)
    {
        var claims = ClaimsOf(caller).ToHashSet(StringComparer.Ordinal);
        foreach (var granted in Parse(grantedClaims))
        {
            claims.Add(granted);
        }

        return claims;
    }
}
