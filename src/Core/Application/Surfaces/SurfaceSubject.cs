namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Who a surface request belongs to. The stable identity is
/// <see cref="Issuer"/> + <see cref="SubjectId"/>, never the subject alone — a
/// different provider may legitimately mint the same subject id, and a consumer that
/// only compares subjects would not notice (ADR-017 §3).
/// </summary>
/// <param name="Issuer">Authority vouching for the subject, for example <c>crm.example</c>.</param>
/// <param name="SubjectId">Identifier stable within that issuer.</param>
public sealed record SurfaceSubject(string Issuer, string SubjectId)
{
    /// <summary>
    /// Collision-free composite key for storage and comparison. The separator cannot
    /// occur in a validated issuer, so two distinct subjects never share a key.
    /// </summary>
    public string Key => $"{Issuer}|{SubjectId}";
}
