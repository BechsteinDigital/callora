using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Callora.Surface.Rendering.Api;

/// <summary>
/// Die Messung des öffentlichen Renderpfads — der einzige Pfad, den Endkunden treffen, und
/// bis dahin der einzige ohne jede Zahl.
/// <para>
/// Die Aufteilung zwischen Metrik und Trace ist Absicht und nicht Geschmack: Die Metrik trägt
/// <b>Workspace, Fläche, Ausgang und Grund</b> — vier Dimensionen mit begrenztem Wertebereich, auf
/// die sich ein Alarm stützen kann. Die Aufschlüsselung, WELCHER der sechs Auflösungsschritte die
/// Zeit verbraucht hat, steht im Trace. Sie als Metrik-Label zu führen hieße, jede Anfrage zu einer
/// eigenen Zeitreihe zu machen.
/// </para>
/// <para>
/// Aus demselben Grund steht die Trace-Id hier NICHT an den Metriken — sie ist pro Anfrage
/// einzigartig, und eine Metrik-Dimension mit unbegrenztem Wertebereich bringt jedes
/// Zeitreihen-Backend um. An der Activity gehört sie hin, dort ist sie die Verbindung zum Log.
/// </para>
/// </summary>
public static class SurfaceRenderTelemetry
{
    public const string ActivitySourceName = "Callora.Surface.Rendering";
    public const string MeterName = "Callora.Surface.Rendering";
    public const string RequestCountMetricName = "callora.surface.render.requests";
    public const string DurationMetricName = "callora.surface.render.duration.ms";

    /// <summary>Kein Fehler — gesetzt, damit das Tag-Schema über beide Ausgänge dasselbe bleibt.</summary>
    public const string ReasonNone = "none";

    /// <summary>Für Host und Pfad gibt es keine Fläche.</summary>
    public const string ReasonRouteNotFound = "route_not_found";

    /// <summary>
    /// Die Fläche existiert, dieser Besucher darf sie nicht sehen. Ausgeliefert wird 404 und nicht
    /// 403 — die Metrik unterscheidet, was die Antwort bewusst verschweigt.
    /// </summary>
    public const string ReasonVisibilityDenied = "visibility_denied";

    /// <summary>
    /// Die Fläche verlangt eine Anmeldung, der Besucher hat keine. Kein Fehler, aber auch keine
    /// ausgelieferte Seite — als eigener Grund geführt, damit ein Alarm auf die Fehlerrate ihn
    /// ausschließen kann, statt jede Anmeldeaufforderung als Störung zu zählen.
    /// </summary>
    public const string ReasonSignInRequired = "sign_in_required";

    /// <summary>
    /// Das Zugriffs-Gate der Fläche hat abgelehnt — getrennt von <see cref="ReasonVisibilityDenied"/>
    /// geführt, weil dort eine Claim-Regel greift und hier die Anmeldung an der Fläche selbst.
    /// </summary>
    public const string ReasonAccessRejected = "access_rejected";

    /// <summary>Ein Pflicht-Contributor kennt die angefragte Adresse nicht (404).</summary>
    public const string ReasonDataMissing = "data_missing";

    /// <summary>Ein Pflicht-Contributor konnte nicht antworten (503) — die Fläche ist gestört.</summary>
    public const string ReasonDataUnavailable = "data_unavailable";

    /// <summary>Der angefragte Unterpfad gehört keiner Fläche, die ihn beansprucht.</summary>
    public const string ReasonPathNotClaimed = "path_not_claimed";

    private static readonly ActivitySource RenderActivitySource = new(ActivitySourceName);
    private static readonly Meter RenderMeter = new(MeterName);

    private static readonly Counter<long> RequestCounter = RenderMeter.CreateCounter<long>(
        RequestCountMetricName,
        unit: "request",
        description: "Counts public surface renders by workspace, surface, outcome and failure reason.");

    private static readonly Histogram<double> RenderDurationMs = RenderMeter.CreateHistogram<double>(
        DurationMetricName,
        unit: "ms",
        description: "Public surface render duration in milliseconds.");

    /// <summary>
    /// Öffnet den Span für eine Anfrage. Host und Pfad stehen darin, weil sie beim Start das
    /// Einzige sind, was bekannt ist — die Fläche ergibt sich erst aus der Auflösung.
    /// </summary>
    public static Activity? StartRender(string host, string path)
    {
        var activity = RenderActivitySource.StartActivity("surface.render", ActivityKind.Server);
        activity?.SetTag("http.request.host", host);
        activity?.SetTag("http.route.path", path);
        return activity;
    }

    /// <summary>
    /// Ein einzelner Auflösungsschritt als Kind-Span. Gibt <c>null</c> zurück, wenn niemand
    /// zuhört — der Aufrufer kann das Ergebnis bedenkenlos in ein <c>using</c> stecken.
    /// </summary>
    public static Activity? StartStep(string step) =>
        RenderActivitySource.StartActivity($"surface.render.{step}", ActivityKind.Internal);

    /// <summary>
    /// Schließt die Messung ab. Wird bei jedem Ausgang gerufen, der eine Fläche betrifft — auch
    /// beim Fehlschlag, sonst zählt die Statistik nur die guten Fälle.
    /// </summary>
    /// <param name="workspaceKey">Leer, wenn keine Fläche aufgelöst werden konnte.</param>
    /// <param name="surfaceKey">Leer, wenn keine Fläche aufgelöst werden konnte.</param>
    /// <param name="reason">Einer der <c>Reason*</c>-Werte, nie ein freier Text.</param>
    public static void CompleteRender(
        Activity? activity,
        string workspaceKey,
        string surfaceKey,
        bool isSuccess,
        string reason,
        long startTimestamp)
    {
        var outcome = isSuccess ? "success" : "failure";

        activity?.SetTag("workspace.key", workspaceKey);
        activity?.SetTag("surface.key", surfaceKey);
        activity?.SetTag("surface.render.outcome", outcome);
        activity?.SetTag("surface.render.reason", reason);
        activity?.SetStatus(
            isSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
            isSuccess ? null : reason);

        TagList tags =
        [
            new KeyValuePair<string, object?>("workspace.key", workspaceKey),
            new KeyValuePair<string, object?>("surface.key", surfaceKey),
            new KeyValuePair<string, object?>("surface.render.outcome", outcome),
            new KeyValuePair<string, object?>("surface.render.reason", reason)
        ];

        RequestCounter.Add(1, tags);
        RenderDurationMs.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds, tags);
    }
}
