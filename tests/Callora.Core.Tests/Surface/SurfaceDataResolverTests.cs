using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Surfaces.Data;
using Callora.Core.Domain.Workspaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Die Regeln um die Daten-Contributoren — und die sind die eigentliche Arbeit. Was ein
/// Contributor beiträgt, steht im ausgelieferten HTML: Wer die Seite abruft, liest es, bei einer
/// Public-Surface ohne Anmeldung.
/// </summary>
public sealed class SurfaceDataResolverTests
{
    private static SurfaceDataRequest Request(SurfaceCaller? caller = null) => new(
        "acme", "shop", "/produkt/schuhe", "de", caller);

    private static SurfaceCaller Caller() =>
        new GuestSurfaceCaller(new SurfaceSubject("idp", "anna"));

    private static SurfaceDataResolver Resolver(params IHostSurfaceDataContributor[] contributors) =>
        new(contributors, NullLogger<SurfaceDataResolver>.Instance);

    // ── Namensraum ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ValuesAppearUnderTheContributorsNamespace()
    {
        var resolver = Resolver(new Stub("catalog", values: new() { ["product"] = "Schuh" }));

        var result = await resolver.ResolveAsync(Request(), SurfaceAuthentication.Public);

        Assert.Equal("Schuh", result.Values["catalog"]["product"]);
    }

    [Fact]
    public async Task TwoContributorsClaimingOneNamespaceDoNotOverwriteEachOther()
    {
        // Still den zweiten gewinnen zu lassen hieße, dass die Seite von der
        // Registrierungsreihenfolge abhängt — die niemand steuert und niemand debuggen kann.
        var resolver = Resolver(
            new Stub("catalog", values: new() { ["product"] = "erster" }),
            new Stub("catalog", values: new() { ["product"] = "zweiter" }));

        var result = await resolver.ResolveAsync(Request(), SurfaceAuthentication.Public);

        Assert.Equal("erster", result.Values["catalog"]["product"]);
    }

    // ── Sichtbarkeit: die Regel wird HIER durchgesetzt ───────────────────────

    [Fact]
    public async Task ACallerSpecificContributorNeverRunsOnAPublicSurface()
    {
        // Sonst läse jeder, der die Seite abruft, was für einen Besucher gedacht war. Der
        // Contributor entscheidet das nicht — er sagt nur, was seine Daten sind.
        var contributor = new Stub(
            "cart", SurfaceDataVisibility.CallerSpecific, values: new() { ["items"] = 3 });
        var resolver = Resolver(contributor);

        var result = await resolver.ResolveAsync(Request(Caller()), SurfaceAuthentication.Public);

        Assert.Empty(result.Values);
        Assert.False(contributor.WasCalled);
    }

    [Fact]
    public async Task ACallerSpecificContributorRunsOnAnAuthenticatedSurface()
    {
        var resolver = Resolver(new Stub(
            "cart", SurfaceDataVisibility.CallerSpecific, values: new() { ["items"] = 3 }));

        var result = await resolver.ResolveAsync(Request(Caller()), SurfaceAuthentication.SurfaceIdentity);

        Assert.Equal(3, result.Values["cart"]["items"]);
    }

    [Fact]
    public async Task ACallerSpecificContributorDoesNotRunWithoutAnEstablishedCaller()
    {
        var contributor = new Stub("cart", SurfaceDataVisibility.CallerSpecific);
        var resolver = Resolver(contributor);

        await resolver.ResolveAsync(Request(caller: null), SurfaceAuthentication.Public);

        Assert.False(contributor.WasCalled);
    }

    // ── Cachebarkeit ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CallerSpecificDataMakesTheResponseUncacheable()
    {
        // Ein Proxy davor lieferte sonst die Daten des ersten Besuchers an alle danach.
        var resolver = Resolver(new Stub(
            "cart", SurfaceDataVisibility.CallerSpecific, values: new() { ["items"] = 3 }));

        var result = await resolver.ResolveAsync(Request(Caller()), SurfaceAuthentication.SurfaceIdentity);

        Assert.False(result.Cacheable);
    }

    [Fact]
    public async Task CallerIndependentDataStaysCacheable()
    {
        var resolver = Resolver(new Stub("catalog", values: new() { ["product"] = "Schuh" }));

        var result = await resolver.ResolveAsync(Request(), SurfaceAuthentication.Public);

        Assert.True(result.Cacheable);
    }

    // ── Drei Ausgänge, nicht zwei ────────────────────────────────────────────



    [Fact]
    public async Task ARequiredContributorThatSaysMissingMeansNotFound()
    {
        var resolver = Resolver(new Stub("catalog", required: true, result: SurfaceDataResult.Missing));

        var result = await resolver.ResolveAsync(Request(), SurfaceAuthentication.Public);

        Assert.Equal("catalog", result.MissingRequiredNamespace);
        Assert.Null(result.FailedRequiredNamespace);
    }

    [Fact]
    public async Task ARequiredContributorThatFailsMeansUnavailable()
    {
        // Ein anderer Ausgang als oben: Das Produkt mag existieren, wir kamen nur nicht heran.
        // 404 dafür hieße, der Kunde denkt, es sei weg.
        var resolver = Resolver(new Stub("catalog", required: true, throws: true));

        var result = await resolver.ResolveAsync(Request(), SurfaceAuthentication.Public);

        Assert.Equal("catalog", result.FailedRequiredNamespace);
        Assert.Null(result.MissingRequiredNamespace);
    }

    [Fact]
    public async Task AnOptionalContributorSayingMissingIsJustNothing()
    {
        var resolver = Resolver(new Stub("empfehlungen", result: SurfaceDataResult.Missing));

        var result = await resolver.ResolveAsync(Request(), SurfaceAuthentication.Public);

        Assert.Empty(result.Values);
        Assert.Null(result.MissingRequiredNamespace);
        Assert.Null(result.FailedRequiredNamespace);
    }

    // ── Ausfall und Zeitbudget ───────────────────────────────────────────────

    [Fact]
    public async Task OneThrowingContributorDoesNotTakeTheOthersWithIt()
    {
        var resolver = Resolver(
            new Stub("kaputt", throws: true),
            new Stub("catalog", values: new() { ["product"] = "Schuh" }));

        var result = await resolver.ResolveAsync(Request(), SurfaceAuthentication.Public);

        Assert.Equal("Schuh", result.Values["catalog"]["product"]);
        Assert.Contains("kaputt", result.Skipped);
    }

    [Fact]
    public async Task AContributorThatOverrunsItsBudgetIsSkipped()
    {
        var resolver = Resolver(
            new Stub("langsam", delay: SurfaceDataResolver.ContributorBudget * 4),
            new Stub("catalog", values: new() { ["product"] = "Schuh" }));

        var result = await resolver.ResolveAsync(Request(), SurfaceAuthentication.Public);

        Assert.Contains("langsam", result.Skipped);
        Assert.Equal("Schuh", result.Values["catalog"]["product"]);
    }

    [Fact]
    public async Task ContributorsRunAtOnceRatherThanInTurn()
    {
        // Fünf zu je fünfzig Millisekunden sind eine Viertelsekunde auf jeder Seite, wenn sie
        // nacheinander laufen.
        //
        // Ohne Zeitmessung geprüft: Jeder Beitrag wartet, bis ALLE gestartet sind. Liefen sie
        // nacheinander, käme der erste nie über diesen Punkt hinaus und risse sein Budget. Eine
        // Stoppuhr hätte dieselbe Aussage nur unter Last verloren — und Last ist genau der
        // Zustand, in dem so ein Test etwas gälte.
        var all = new TaskCompletionSource();
        var started = 0;

        async Task<SurfaceDataResult> WaitForTheOthers(CancellationToken token)
        {
            if (Interlocked.Increment(ref started) == 3)
            {
                all.SetResult();
            }

            await all.Task.WaitAsync(token).ConfigureAwait(false);
            return SurfaceDataResult.Contributed(
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["x"] = 1 });
        }

        var resolver = Resolver(
            new Stub("a", contribute: WaitForTheOthers),
            new Stub("b", contribute: WaitForTheOthers),
            new Stub("c", contribute: WaitForTheOthers));

        var result = await resolver.ResolveAsync(Request(), SurfaceAuthentication.Public);

        Assert.Equal(3, result.Values.Count);
        Assert.Empty(result.Skipped);
    }

    private sealed class Stub(
        string ns,
        SurfaceDataVisibility visibility = SurfaceDataVisibility.CallerIndependent,
        bool required = false,
        Dictionary<string, object?>? values = null,
        SurfaceDataResult? result = null,
        bool throws = false,
        TimeSpan? delay = null,
        Func<CancellationToken, Task<SurfaceDataResult>>? contribute = null) : IHostSurfaceDataContributor
    {
        public string Namespace => ns;

        public SurfaceDataVisibility Visibility => visibility;

        public bool Required => required;

        public bool WasCalled { get; private set; }

        public async Task<SurfaceDataResult> ContributeAsync(
            SurfaceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            if (contribute is not null)
            {
                return await contribute(cancellationToken).ConfigureAwait(false);
            }

            if (delay is { } wait)
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }

            if (throws)
            {
                throw new InvalidOperationException("Der Katalog antwortet nicht.");
            }

            return result
                ?? (values is null
                    ? SurfaceDataResult.Nothing
                    : SurfaceDataResult.Contributed(values));
        }
    }
}
