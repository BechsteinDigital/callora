using Callora.Core.Application.Surfaces.Layout;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Jede Methode von <see cref="ISurfaceLayoutSource"/> führt ihren Workspace selbst.
/// <para>
/// <b>Warum als Regel und nicht als Verhaltenstest:</b> <c>GetDraftAsync</c> stand hier lange
/// ohne <c>workspaceKey</c> — und hatte im Kern keinen einzigen Aufrufer. Genau deshalb fiel es
/// nicht auf: Ein Verhaltenstest braucht einen Aufrufer, den es nicht gab, und der erste, den
/// jemand geschrieben hätte, hätte die fehlende Eingrenzung geerbt. Der Layout-Schlüssel IST der
/// Flächenschlüssel, und <c>kontakt</c> heißt bei jedem zweiten Mandanten so; eine Implementierung
/// ohne Workspace liefert deshalb, was sie zuerst findet.
/// </para>
/// <para>
/// Die Regel gilt für die vierte Methode, die jemand morgen hinzufügt, genauso — und dann ist sie
/// es wert, erzwungen zu werden.
/// </para>
/// </summary>
public sealed class SurfaceLayoutSourceContractTests
{
    [Fact]
    public void EveryMethodTakesItsWorkspaceKeyFirst()
    {
        var offenders = typeof(ISurfaceLayoutSource)
            .GetMethods()
            .Where(method => method.GetParameters().FirstOrDefault() is not { Name: "workspaceKey" } first
                             || first.ParameterType != typeof(string))
            .Select(method => method.Name)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Diese Methoden können ihren Mandanten nicht eingrenzen — ein workspaceKey als erster "
            + "Parameter gehört in den Vertrag, nicht an die Aufrufstelle:\n"
            + string.Join('\n', offenders));
    }
}
