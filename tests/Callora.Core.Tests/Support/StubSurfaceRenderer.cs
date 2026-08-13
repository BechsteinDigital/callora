using Callora.Surface.Rendering;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Ein Renderer, der sofort antwortet — für Tests, die den Weg DURCH den Endpunkt prüfen und
/// nicht die Template-Engine.
/// <para>
/// Er existiert aus einem konkreten Anlass: Jint ist auf zwei Sekunden je Render begrenzt
/// (<c>NunjucksSurfaceRenderer.TimeoutSeconds</c>), und der erste echte Render in einem
/// Testprozess braucht im Debug-Build ein Vielfaches davon — der Interpreter wird dabei erst
/// jitgekompiliert. Mehrere echte Renders hintereinander reißen die Grenze deshalb sporadisch,
/// und der Test scheitert an der Härtung statt an seiner Aussage.
/// </para>
/// <para>
/// Die Grenze bleibt, wie sie ist: Zwei Sekunden sind eine bewusste Entscheidung gegen ein
/// Template, das den Anfragethread festhält. Sie wegen des Testverhaltens anzuheben hieße, eine
/// Schutzmaßnahme nach dem auszurichten, was am lautesten stört.
/// </para>
/// </summary>
public sealed class StubSurfaceRenderer : ISurfaceRenderer
{
    public SurfaceRenderContext? LastContext { get; private set; }

    public string Render(string templateText, SurfaceRenderContext context)
    {
        LastContext = context;
        return "<html><!-- stub --></html>";
    }

    public string Render(string templateText, SurfaceRenderContext context, IReadOnlyList<string> bundleChain)
    {
        LastContext = context;
        return "<html><!-- stub --></html>";
    }
}
