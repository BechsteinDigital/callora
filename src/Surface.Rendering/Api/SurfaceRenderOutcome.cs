using Microsoft.AspNetCore.Http;

namespace Callora.Surface.Rendering.Api;

/// <summary>
/// Was beim Rendern herauskam — die Antwort und das, was die Messung darüber wissen muss.
/// <para>
/// Er existiert, damit der Renderkern seine sieben Ausgänge behalten kann und trotzdem jeder
/// davon gezählt wird. Die Alternative wäre ein <c>try/finally</c> um zweihundertfünfzig Zeilen
/// gewesen: Dann hinge die Vollständigkeit der Messung daran, dass jeder künftige Ausgang eine
/// lokale Variable richtig setzt, bevor er zurückkehrt — und ein vergessener wäre unsichtbar,
/// weil die Messung trotzdem eine Zahl liefert, nur die falsche.
/// </para>
/// </summary>
/// <param name="Result">Die HTTP-Antwort, unverändert.</param>
/// <param name="IsSuccess">Ob eine Seite ausgeliefert wurde. Ein 404 auf eine unbekannte Adresse
/// ist kein Erfolg, auch wenn er korrekt ist.</param>
/// <param name="Reason">Einer der <c>SurfaceRenderTelemetry.Reason*</c>-Werte, nie freier Text —
/// der Wert wird zur Metrik-Dimension.</param>
/// <param name="WorkspaceKey">Leer, solange keine Fläche aufgelöst ist.</param>
/// <param name="SurfaceKey">Leer, solange keine Fläche aufgelöst ist.</param>
internal sealed record SurfaceRenderOutcome(
    IResult Result,
    bool IsSuccess,
    string Reason,
    string WorkspaceKey,
    string SurfaceKey)
{
    /// <summary>Eine ausgelieferte Seite.</summary>
    public static SurfaceRenderOutcome Rendered(IResult result, string workspaceKey, string surfaceKey) =>
        new(result, IsSuccess: true, SurfaceRenderTelemetry.ReasonNone, workspaceKey, surfaceKey);

    /// <summary>Ein Fehlschlag mit Grund, an einer Stelle, an der die Fläche schon feststeht.</summary>
    public static SurfaceRenderOutcome Failed(
        IResult result,
        string reason,
        string workspaceKey,
        string surfaceKey) =>
        new(result, IsSuccess: false, reason, workspaceKey, surfaceKey);

    /// <summary>Ein Fehlschlag, bevor überhaupt eine Fläche bekannt war.</summary>
    public static SurfaceRenderOutcome FailedBeforeResolution(IResult result, string reason) =>
        new(result, IsSuccess: false, reason, string.Empty, string.Empty);
}
