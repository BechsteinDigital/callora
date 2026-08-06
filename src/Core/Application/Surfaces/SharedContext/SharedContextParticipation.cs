namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// One subject's part in a conversation anchor, and how much of a value they therefore see.
/// <para>
/// A subject anchor needs none of this: it describes its own subject, who sees all of it. A
/// conversation connects different people with different standing — the agent handling the call
/// and the customer on it — and that difference is what the projection reads.
/// </para>
/// </summary>
/// <param name="Issuer">Identity provider of <paramref name="SubjectId"/>.</param>
/// <param name="SubjectId">Who takes part.</param>
/// <param name="Visibility">How much of a value they receive.</param>
public sealed record SharedContextParticipation(
    string Issuer,
    string SubjectId,
    SharedContextVisibility Visibility);
