namespace Callora.Core.Application.Workspaces.Contracts;

/// <summary>
/// Was ein Plugin am Seitenbaum eines Workspaces tun darf: lesen, anlegen oder ändern,
/// löschen.
/// </summary>
/// <remarks>
/// Ein Editor für Flächen braucht den Baum — der gehört aber dem Workspace, nicht dem
/// Plugin. <see cref="IWorkspaceSurfaceStore"/> selbst zu veröffentlichen wäre der kürzere
/// Weg gewesen und der falsche: Er trägt neben diesen drei Operationen auch
/// <c>AssignIdentityProviderAsync</c>, und wer einer Fläche einen Identity-Provider
/// zuweist, entscheidet, wer sich dort anmelden darf. Das ist keine Editor-Aufgabe.
///
/// <para>
/// Dieser Vertrag ist deshalb der Schnitt entlang dessen, was ein Editor tatsächlich
/// benutzt, nicht entlang dessen, was der Host zufällig an einer Stelle gebündelt hat.
/// Kommt ein Plugin mit einem weitergehenden Bedarf, gehört darüber neu entschieden — nicht
/// hier eine Methode angehängt.
/// </para>
///
/// <para>
/// Der Workspace steht in jedem Aufruf, statt beim Auflösen gebunden zu werden: Ein Plugin
/// löst seine Dienste beim Start auf, der Workspace steht aber erst pro Anfrage fest. Wer
/// welchen Workspace nennen darf, entscheidet ohnehin der Host, bevor er einen
/// Plugin-Handler überhaupt aufruft — ein an einen Workspace gebundener Admin behält seinen,
/// nur ein Plattform-Operator darf einen wählen.
/// </para>
/// </remarks>
public interface ISurfaceTreeEditor
{
    /// <summary>Alle Flächen des Workspaces, nach Schlüssel geordnet.</summary>
    Task<IReadOnlyList<WorkspaceSurfaceSnapshot>> ListAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>Eine einzelne Fläche, oder <c>null</c>.</summary>
    Task<WorkspaceSurfaceSnapshot?> GetAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>Legt eine Fläche an oder ändert sie.</summary>
    Task<WorkspaceSurfaceSnapshot?> UpsertAsync(
        string workspaceKey,
        WorkspaceSurfaceInput input,
        CancellationToken cancellationToken = default);

    /// <summary>Löscht eine Fläche.</summary>
    Task<SurfaceDeleteResult> DeleteAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default);
}
