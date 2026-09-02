using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Welche Berechtigungen von welchem Plugin kommen.
/// </summary>
/// <remarks>
/// <para>
/// Die eine Stelle, die beide Zulieferwege zusammenführt und dabei weiß, wem was gehört. Ein Plugin
/// deklariert seine Schlüssel im Manifest oder steuert sie über
/// <c>IHostAdminApiExtensionContributor</c> bei; von den heute installierten Plugins nutzt genau eines
/// den ersten Weg. Wer nur eine Quelle liest, übergeht die anderen — und zwar wortlos, weil eine leere
/// Schlüsselliste sich von „hat keine Berechtigungen" nicht unterscheiden lässt.
/// </para>
/// <para>
/// Der Zuschnitt entstand, als ein zweiter Verwender kam: Die Rollenanlage brauchte die Zuordnung
/// bereits, und die Sitzung eines Workspace-Admins braucht sie jetzt auch. Vorher stand sie in der
/// Rollenanlage — eine zweite Fassung daneben wäre eine zweite Antwort auf dieselbe Frage.
/// </para>
/// </remarks>
[CalloraInternal("Berechtigungsherkunft — Durchsetzung, kein Plugin-Vertrag (REV2 §7.2)")]
public interface IPluginPermissionMap
{
    /// <summary>Je Plugin seine Schlüssel, sortiert und ohne Dubletten.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ByPluginAsync(
        CancellationToken cancellationToken = default);
}
