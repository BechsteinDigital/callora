using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Surface.Rendering.Api;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Wer auf einer Fläche beitragen darf.
/// </summary>
/// <remarks>
/// Die UI-Kette enthält für eine Fläche ohne App jedes im Workspace aktive Plugin. Deshalb zeigte
/// eine Inhaltsseite ohne einen einzigen Block die Navigation fremder Anwendungen — die
/// Videokonferenz stand im Menü einer Seite, die sie nie erwähnt.
///
/// <para>
/// Das Layout ist die genauere Auskunft als jede gepflegte Liste: Es sagt, was gebraucht wird,
/// und kann nicht veralten.
/// </para>
/// </remarks>
public sealed class OnlyWhatTheSurfaceNeedsContributesTests
{
    [Fact]
    public void AnEmptyLayoutMeansNobodyContributes()
    {
        // Der Fall aus dem Fehlerbericht: eine Fläche ohne Sektion, ohne Block — und trotzdem
        // stand „Video conferences" in ihrer Navigation.
        var narrowed = SurfaceContributors.OnThisSurface(
            ["communication", "videoconference", "composer"],
            Surface(app: null),
            usedBlockIds: []);

        Assert.Empty(narrowed);
    }

    [Fact]
    public void OnlyThePluginsWhoseBlocksAreUsed()
    {
        var narrowed = SurfaceContributors.OnThisSurface(
            ["communication", "videoconference", "composer"],
            Surface(app: null),
            ["communication.incoming-call", "communication.phone"]);

        Assert.Equal(["communication"], narrowed);
    }

    [Fact]
    public void TheThemeStaysEvenWithoutABlock()
    {
        // Es gestaltet, es rendert nicht. Eine Seite ohne Theme sähe nicht nach weniger Inhalt
        // aus, sondern nach einem Fehler.
        var narrowed = SurfaceContributors.OnThisSurface(
            ["acme.theme", "videoconference"],
            Surface(app: null, theme: "acme.theme"),
            usedBlockIds: []);

        Assert.Equal(["acme.theme"], narrowed);
    }

    [Fact]
    public void ASurfaceWithAnAppKeepsItsChainUntouched()
    {
        // Dort hat die Kettenauflösung schon entschieden — sie endet bei der App. Hier noch
        // einmal zu kürzen hieße, dieselbe Frage zweimal zu beantworten.
        var narrowed = SurfaceContributors.OnThisSurface(
            ["videoconference", "acme.theme"],
            Surface(app: "videoconference"),
            usedBlockIds: []);

        Assert.Equal(["videoconference", "acme.theme"], narrowed);
    }

    private static WorkspaceSurfaceSnapshot Surface(string? app, string? theme = null) =>
        new(
            Guid.NewGuid(),
            "acme",
            "start",
            "Start",
            "spa",
            null,
            null,
            "/",
            SurfaceAccessMode.Public,
            SurfaceRouting.Tree,
            "de",
            app,
            app is null ? null : "1.0.0",
            theme,
            theme is null ? null : "1.0.0",
            true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
