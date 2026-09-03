namespace Callora.Core.Domain.Plugins;

/// <summary>
/// Ob die Workspaces eines Mandanten ein lizenziertes Plugin selbst zuweisen dürfen.
/// </summary>
/// <remarks>
/// <para>
/// <b>Die Lücke, die das schließt.</b> Der Bezug ist dreistufig: Der Instanzbetreiber entscheidet
/// per <see cref="Callora.Core.Domain.Entitlements.PluginEntitlement"/>, welche Plugins ein Mandant
/// überhaupt nutzen darf; der Mandant weist sie seinen Workspaces zu. Die dritte Frage — darf ein
/// Workspace-Administrator sich selbst bedienen — hatte keinen Ort. Ohne sie hieße
/// <c>plugin.assign</c> im Workspace-Satz: Jeder Workspace nimmt sich, was der Mandant lizenziert
/// hat, und der Mandant kann nicht nein sagen.
/// </para>
/// <para>
/// <b>Zwei Zustände, nicht drei.</b> Slack Enterprise Grid kennt org-installiert / freigegeben /
/// gesperrt, weil dort der Standard durchlässig ist und „gesperrt" ihn zurücknimmt. Hier ist der
/// Standard geschlossen: Ohne Zeile darf nur der Mandant zuweisen. Ein dritter Zustand „gesperrt"
/// unterschiede sich davon nur dadurch, dass er auch den Mandanten selbst aussperrt — ein Schloss
/// gegen sich selbst, für das niemand einen Fall genannt hat. Er wäre additiv, wenn doch.
/// </para>
/// <para>
/// <b>Kein Workspace im Schlüssel, und das ist die Aussage.</b> Die Entscheidung gilt für alle
/// Workspaces des Mandanten. Wäre sie je Workspace zu treffen, wäre sie dieselbe Zuweisung noch
/// einmal — nur in einer zweiten Tabelle, die der ersten widersprechen kann.
/// </para>
/// </remarks>
public sealed class TenantPluginDelegation
{
    public Guid Id { get; set; }

    public string TenantKey { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// True, wenn ein Workspace-Administrator dieses Plugin seinem eigenen Workspace zuweisen darf.
    /// Fehlt die Zeile, gilt <c>false</c> — der Mandant behält die Entscheidung.
    /// </summary>
    public bool WorkspacesMayAssign { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
