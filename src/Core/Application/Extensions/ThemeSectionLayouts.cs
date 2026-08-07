namespace Callora.Core.Application.Extensions;

/// <summary>
/// Was ein Theme selbst zu Sektionslayouts erklärt hat — noch ohne die Basis.
/// </summary>
/// <param name="Layouts">Die eigenen Layouts, in deklarierter Reihenfolge.</param>
/// <param name="InheritsBase">
/// Ob die Layouts des Basis-Themes dazukommen. Standard ist ja, und das ist die sichere
/// Richtung: Das Basis-Stylesheet der Runtime ist immer geladen, also funktionieren die
/// Basis-Layouts auch unter einem fremden Theme.
/// </param>
public sealed record ThemeSectionLayouts(
    IReadOnlyList<SectionLayoutDefinition> Layouts,
    bool InheritsBase);
