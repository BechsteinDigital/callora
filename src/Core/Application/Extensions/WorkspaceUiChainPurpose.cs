namespace Callora.Core.Application.Extensions;

/// <summary>
/// Wofür eine UI-Ladekette aufgelöst wird. Der Unterschied ist keine Feinheit: dieselbe
/// Fläche hat zwei richtige Antworten, je nachdem, ob sie gerade angezeigt oder bearbeitet
/// wird.
/// </summary>
public enum WorkspaceUiChainPurpose
{
    /// <summary>
    /// Anzeigen. Eine Inhaltsfläche lädt, was ihr veröffentlichtes Layout verlangt — und
    /// sonst nichts, damit sich nicht jedes aktive Plugin in jede Fläche hineinrendert.
    /// Das ist der Standard und die einzige Kette, die ein anonymer Aufrufer bekommt.
    /// </summary>
    Render,

    /// <summary>
    /// Bearbeiten. Was auf dieser Fläche eingebaut werden KÖNNTE, nicht was schon drin ist.
    /// <para>
    /// Ohne diesen Zweck ist der Editor ein Henne-Ei-Problem: die Block-Palette braucht das
    /// Bundle eines Plugins, um dessen Blöcke anzubieten, und die Render-Kette liefert das
    /// Bundle erst, wenn einer seiner Blöcke bereits im veröffentlichten Layout steht. Eine
    /// leere Fläche könnte damit nie eine erste Block-Wahl anbieten.
    /// </para>
    /// <para>
    /// Die Flächenzuordnung bleibt: gehört die Fläche einer Anwendung, endet die Kette auch
    /// hier bei ihr. In einen Konferenzraum baut niemand Telefon-Blöcke, auch nicht im
    /// Editor. Gekürzt wird nur die Layout-Bedingung.
    /// </para>
    /// </summary>
    Catalog,
}
