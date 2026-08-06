namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// A shared context key as a documented contract rather than a string.
/// <para>
/// Declared on the SERVER, not in the browser. The client-side descriptor (key, publisher,
/// cardinality) is enough for the local channel, where a value never leaves the tab. A shared
/// value crosses surfaces and sessions and carries personal data, so it needs what a local one
/// does not: which anchor bears it, what purpose it serves, which fields travel how far, and how
/// long it lives.
/// </para>
/// <para>
/// With the declaration in one place, the projection per subscriber follows from it instead of
/// being reinvented in every publisher — and a publisher that forgets is not a data leak but a
/// field nobody sees.
/// </para>
/// </summary>
/// <param name="Key">Namespaced and versioned, e.g. <c>communication.active-call/v1</c>.</param>
/// <param name="AnchorType">What bears this key — see <see cref="SharedContextAnchorType"/>.</param>
/// <param name="Purpose">
/// Why this data is shared, in plain words. GDPR purpose limitation, and the sentence an operator
/// reads when asked what leaves their system.
/// </param>
/// <param name="Fields">
/// The fields of the value and how far each travels. A field that is NOT declared is not
/// published at all — an undeclared field cannot be projected, so shipping it would mean
/// shipping something nobody described.
/// </param>
/// <param name="TimeToLive">
/// How long a published value stays readable. An "active call" must not hang forever because a
/// tab crashed; storage limitation is not optional for personal data (§5.4).
/// </param>
/// <param name="PublisherPluginId">Who may publish under this key. Nobody else can.</param>
public sealed record SharedContextKeyDeclaration(
    string Key,
    SharedContextAnchorType AnchorType,
    string Purpose,
    IReadOnlyList<SharedContextFieldDeclaration> Fields,
    TimeSpan TimeToLive,
    string PublisherPluginId)
{
    /// <summary>
    /// The fields a subscriber at <paramref name="visibility"/> receives. An owner sees every
    /// declared field, a participant only those marked as such.
    /// </summary>
    public IReadOnlyList<SharedContextFieldDeclaration> FieldsFor(SharedContextVisibility visibility) =>
        visibility == SharedContextVisibility.Owner
            ? Fields
            : [.. Fields.Where(field => field.Visibility == SharedContextVisibility.Participant)];
}
