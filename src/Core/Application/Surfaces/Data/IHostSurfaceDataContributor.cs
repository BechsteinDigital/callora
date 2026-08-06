using Callora.Core.Extensibility;

namespace Callora.Core.Application.Surfaces.Data;

/// <summary>
/// Contributes data a surface template can read — a product for <c>/produkt/schuhe</c>, the
/// opening hours for <c>/kontakt</c>.
/// <para>
/// The counterpart to <c>IHostSurfaceViewContributor</c>: that one contributes what is rendered,
/// this one what it is rendered from. Called after the surface is resolved, with the path within
/// it, so a contributor can tell <c>/produkt/schuhe</c> from <c>/warenkorb</c> — without the path
/// it could only ever answer surface-globally.
/// </para>
/// <para>
/// <b>What comes back must be JSON-serialisable.</b> The sandbox boundary holds: the template
/// engine never sees a .NET type, only a document. A contributor that hands back an entity hands
/// back whatever that entity serialises to, including the parts nobody meant to publish — so
/// build the shape you mean to expose.
/// </para>
/// <para>
/// <b>Everything contributed reaches the delivered HTML.</b> Whoever fetches the page reads it —
/// on a Public surface without signing in. That is why <see cref="Visibility"/> is a declaration
/// and not a comment: the host acts on it, rather than trusting every contributor to remember
/// which kind of surface it landed on.
/// </para>
/// </summary>
[CalloraExtensible("Extension point — implement to contribute data to server-rendered surfaces")]
public interface IHostSurfaceDataContributor
{
    /// <summary>
    /// Namespace the values appear under, e.g. <c>catalog</c> → <c>{{ data.catalog.product }}</c>.
    /// Conventionally the plugin id. Two contributors claiming one namespace would overwrite each
    /// other, so the host keeps the first and reports the second rather than picking silently.
    /// </summary>
    string Namespace { get; }

    /// <summary>Whether the contribution depends on who is looking. The host acts on it.</summary>
    SurfaceDataVisibility Visibility { get; }

    /// <summary>
    /// Whether the page makes sense without this data.
    /// <para>
    /// A contributor that times out or throws is normally skipped and the page renders without
    /// it. For a product page that is wrong: a page without its product looks complete and is
    /// false, which is worse than an error. Setting this turns a failure into a failed request
    /// instead of a misleading page — and lets <see cref="SurfaceDataResult.Missing"/> mean 404
    /// while a failure means 503.
    /// </para>
    /// </summary>
    bool Required { get; }

    /// <summary>
    /// What this contributor has to say — values, nothing, or "that does not exist".
    /// </summary>
    /// <remarks>
    /// <b>Do not read another contributor's data here.</b> They run at once and each has its own
    /// budget; the moment one waits for another, the budget stops being parallel and the failure
    /// rules turn transitive — if A drops out, B goes quietly WRONG instead of empty, which is
    /// the failure mode hardest to notice. A contributor that needs another plugin's data takes
    /// that plugin's contract, not its render contribution.
    /// </remarks>
    Task<SurfaceDataResult> ContributeAsync(
        SurfaceDataRequest request,
        CancellationToken cancellationToken = default);
}
