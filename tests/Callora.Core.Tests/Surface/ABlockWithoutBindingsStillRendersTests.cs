using System.Text.Json;
using Callora.Core.Application.Surfaces.Layout;
using Callora.Surface.Rendering.Rendering.Composition;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Ein Block ohne gebundene Steuerelemente hat eine LEERE Zuordnung, keine fehlende.
/// </summary>
/// <remarks>
/// Der Editor schreibt <c>config</c> nur, wenn etwas gebunden ist. Ein frisch platzierter Block
/// steht deshalb als <c>{"blockId":"…","region":"main","position":0}</c> im Dokument — und beim
/// Deserialisieren kam <c>Config</c> als <see langword="null"/> zurück.
///
/// <para>
/// Der Kompositions-Renderer lief damit beim ERSTEN Block in eine NullReferenceException: 500
/// auf jeder Seite, die jemand gebaut hatte. Mit einer Trace-ID, ohne Hinweis auf das Layout —
/// und in der Zwischenzeit sah es aus, als käme die veröffentlichte Seite gar nicht erst am
/// Renderpfad an.
/// </para>
/// </remarks>
public sealed class ABlockWithoutBindingsStillRendersTests
{
    // Wortgleich aus der Datenbank einer Installation, in der es geknallt hat.
    private const string StoredDocument = """
        {
          "sections": [
            {
              "blocks": [{ "region": "main", "blockId": "communication.phone", "position": 0 }],
              "layout": "single",
              "position": 0
            }
          ]
        }
        """;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void TheDocumentTheEditorWritesRendersWithoutBindings()
    {
        var document = JsonSerializer.Deserialize<SurfaceLayoutDocument>(StoredDocument, Options);

        Assert.NotNull(document);

        var markup = new SurfaceCompositionRenderer().Render(document!);

        Assert.Contains("data-callora-island=\"communication.phone\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-cal-layout=\"single\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingConfigBecomesAnEmptyOne()
    {
        // Der Standardwert sitzt am VERTRAG, nicht als Null-Check im Serializer: Sonst müsste ihn
        // jede weitere Stelle wiederholen, die einen Block anfasst — und die erste, die es
        // vergisst, bringt wieder eine ganze Seite zu Fall.
        var block = new SurfaceLayoutBlock("demo.block", "main", 0);

        Assert.NotNull(block.Config);
        Assert.Empty(block.Config);
    }

    [Fact]
    public void ExplicitBindingsSurvive()
    {
        // Gegenprobe: Der Standardwert ersetzt nichts, was da ist.
        var bindings = new Dictionary<string, SurfaceBlockBinding>(StringComparer.Ordinal)
        {
            ["title"] = new("static", "Hallo"),
        };

        var block = new SurfaceLayoutBlock("demo.block", "main", 0, bindings);

        Assert.Single(block.Config);
        Assert.Equal("static", block.Config["title"].Source);
    }
}
