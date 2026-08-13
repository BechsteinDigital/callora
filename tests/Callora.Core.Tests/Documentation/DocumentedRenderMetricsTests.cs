using Callora.Core.Tests.Cli;
using Callora.Surface.Rendering.Api;
using System.Reflection;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Hält das Runbook ehrlich über die Renderpfad-Metriken, die es zum Alarmieren empfiehlt.
/// </summary>
/// <remarks>
/// <para>
/// Der Abschnitt „Surface render failure / degradation" hat lange eine Diagnose beschrieben und
/// selbst dazugeschrieben, dass die Zahlen dafür fehlen. Jetzt gibt es sie — und damit beginnt die
/// Sorte Drift, die dieses Repository schon einmal teuer bezahlt hat: Ein Metrikname wird im Code
/// geändert, das Runbook nennt weiter den alten, und im Ernstfall sucht jemand nach einer
/// Zeitreihe, die es nicht mehr gibt. Ein Compiler liest kein Markdown.
/// </para>
/// <para>
/// Geprüft wird in beide Richtungen. Dass jeder dokumentierte Name im Code existiert, fängt den
/// häufigen Fall (Umbenennung). Dass jeder Grund aus dem Code auch im Runbook steht, fängt den
/// selteneren und unangenehmeren: ein neuer Fehlergrund, den niemand in der Tabelle nachträgt —
/// er taucht dann im Dashboard auf, ohne dass jemand sagen kann, was er bedeutet.
/// </para>
/// </remarks>
public sealed class DocumentedRenderMetricsTests
{
    private static readonly string RunbookPath =
        Path.Combine(ScaffoldedPluginFixture.ResolveRepositoryRoot(), "docs-site", "maintainer", "runbooks.md");

    [Fact]
    public void EveryMetricNameInTheRunbookExistsInTheCode()
    {
        var runbook = File.ReadAllText(RunbookPath);

        Assert.Contains(SurfaceRenderTelemetry.RequestCountMetricName, runbook, StringComparison.Ordinal);
        Assert.Contains(SurfaceRenderTelemetry.DurationMetricName, runbook, StringComparison.Ordinal);
        Assert.Contains(SurfaceRenderTelemetry.MeterName, runbook, StringComparison.Ordinal);
        Assert.Contains(SurfaceRenderTelemetry.ActivitySourceName, runbook, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryFailureReasonIsDocumented()
    {
        var runbook = File.ReadAllText(RunbookPath);

        var undocumented = ReasonConstants()
            .Where(reason => !runbook.Contains($"`{reason.Value}`", StringComparison.Ordinal))
            .Select(reason => $"{reason.Name} = \"{reason.Value}\"")
            .ToArray();

        Assert.True(
            undocumented.Length == 0,
            "Diese Fehlergründe stehen im Code, aber nicht in der Runbook-Tabelle — ein Grund ohne "
            + "Bedeutung ist im Dashboard schlimmer als keiner:\n" + string.Join('\n', undocumented));
    }

    /// <summary>
    /// Die Tag-Namen tragen die Alarme: Wer auf ihnen gruppiert, verlässt sich darauf, dass sie
    /// heißen, wie sie im Runbook stehen.
    /// </summary>
    [Fact]
    public void TheTagNamesAreDocumented()
    {
        var runbook = File.ReadAllText(RunbookPath);

        foreach (var tag in new[] { "workspace.key", "surface.key", "surface.render.outcome", "surface.render.reason" })
        {
            Assert.Contains($"`{tag}`", runbook, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<(string Name, string Value)> ReasonConstants() =>
        typeof(SurfaceRenderTelemetry)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false })
            .Where(field => field.Name.StartsWith("Reason", StringComparison.Ordinal))
            .Select(field => (field.Name, (string)field.GetRawConstantValue()!));
}
