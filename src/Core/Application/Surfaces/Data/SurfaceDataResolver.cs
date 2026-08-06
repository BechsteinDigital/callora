using Callora.Core.Domain.Workspaces;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Application.Surfaces.Data;

/// <summary>
/// Runs the data contributors for one request and applies the rules around them.
/// <para>
/// The rules are the substance here, not the calling. Five contributors at fifty milliseconds
/// each are a quarter second on every page if they run in turn, so they run at once. One that
/// hangs must not hold the page, so each gets a budget. One that throws must not take the page
/// with it — the same shape as the template fallback, which degrades a broken plugin view to the
/// SPA shell rather than to a stack trace.
/// </para>
/// <para>
/// And the visibility rules are enforced HERE. A contributor declares whether its data depends on
/// the caller; this decides what follows from that. Leaving it to the contributor would make it
/// discipline, and discipline does not survive a plugin written by somebody who never saw the
/// public surface it ended up on.
/// </para>
/// </summary>
public sealed class SurfaceDataResolver
{
    /// <summary>
    /// How long one contributor may take. Generous enough for a database round trip, short enough
    /// that a page never waits on something a visitor cannot see anyway.
    /// </summary>
    public static readonly TimeSpan ContributorBudget = TimeSpan.FromMilliseconds(500);

    private readonly IReadOnlyList<IHostSurfaceDataContributor> _contributors;
    private readonly ILogger<SurfaceDataResolver> _logger;

    public SurfaceDataResolver(
        IEnumerable<IHostSurfaceDataContributor> contributors,
        ILogger<SurfaceDataResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(logger);
        _contributors = [.. contributors];
        _logger = logger;
    }

    /// <summary>Collects everything this surface, path and caller are entitled to.</summary>
    public async Task<SurfaceDataComposition> ResolveAsync(
        SurfaceDataRequest request,
        SurfaceAccessMode accessMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var eligible = _contributors.Where(c => IsEligible(c, accessMode, request.Caller)).ToArray();
        if (eligible.Length == 0)
        {
            return SurfaceDataComposition.Empty;
        }

        var results = await Task
            .WhenAll(eligible.Select(c => InvokeAsync(c, request, cancellationToken)))
            .ConfigureAwait(false);

        var values = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        var skipped = new List<string>();
        var cacheable = true;
        string? failedRequired = null;
        string? missingRequired = null;

        foreach (var result in results)
        {
            // Konnte nicht antworten — geworfen oder Budget gerissen. Das Ding mag existieren,
            // wir kamen nur nicht heran: 503, nicht 404.
            if (result.Failed)
            {
                if (result.Contributor.Required)
                {
                    failedRequired ??= result.Contributor.Namespace;
                }
                else
                {
                    skipped.Add(result.Contributor.Namespace);
                }

                continue;
            }

            // Hat geantwortet: es gibt das hier nicht. Für einen erforderlichen Beitrag ist das
            // eine 404 — für einen optionalen dasselbe wie nichts zu sagen.
            if (result.Result?.NotFound == true)
            {
                if (result.Contributor.Required)
                {
                    missingRequired ??= result.Contributor.Namespace;
                }

                continue;
            }

            if (result.Result?.Values is not { Count: > 0 } contributed)
            {
                continue;
            }

            // First one wins, and the second is reported. Picking silently would make what a page
            // shows depend on registration order, which nobody controls and nobody can debug.
            if (!values.TryAdd(result.Contributor.Namespace, contributed))
            {
                _logger.LogWarning(
                    "Two surface data contributors claim the namespace {Namespace}; the later one was ignored.",
                    result.Contributor.Namespace);
                continue;
            }

            if (result.Contributor.Visibility == SurfaceDataVisibility.CallerSpecific)
            {
                cacheable = false;
            }
        }

        return new SurfaceDataComposition(
            values, cacheable, failedRequired, missingRequired, skipped);
    }

    /// <summary>
    /// Whether this contributor runs at all. A caller-specific one is not invoked on a Public
    /// surface — anyone who fetches the page would read what it produced — and not without an
    /// established caller, where it has nobody to answer about.
    /// </summary>
    private static bool IsEligible(
        IHostSurfaceDataContributor contributor,
        SurfaceAccessMode accessMode,
        SurfaceCaller? caller) =>
        contributor.Visibility == SurfaceDataVisibility.CallerIndependent ||
        (accessMode != SurfaceAccessMode.Public && caller is not null);

    private async Task<ContributorResult> InvokeAsync(
        IHostSurfaceDataContributor contributor,
        SurfaceDataRequest request,
        CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ContributorBudget);

        try
        {
            var result = await contributor.ContributeAsync(request, budget.Token).ConfigureAwait(false);
            return new ContributorResult(contributor, result, Failed: false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Surface data contributor {Namespace} exceeded its {Budget} budget; rendering without it.",
                contributor.Namespace,
                ContributorBudget);
            return new ContributorResult(contributor, null, Failed: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never the values, only that it failed — a contributor's exception message may carry
            // what it was working on (§5.5 P6).
            _logger.LogWarning(
                ex,
                "Surface data contributor {Namespace} failed; rendering without it.",
                contributor.Namespace);
            return new ContributorResult(contributor, null, Failed: true);
        }
    }
}
