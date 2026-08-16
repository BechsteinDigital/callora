import { readonly, ref } from 'vue'

/**
 * Ob der Versuch, die Plugin-Bundles zu laden, vorbei ist — geglückt oder nicht (#296).
 *
 * Ohne diese Unterscheidung gibt es nur zwei Zustände, und beide sehen gleich aus: „noch nicht
 * geladen" und „lädt nie". Eine Insel, deren Bundle ausfällt, blieb deshalb ein leeres `div` ohne
 * Text und ohne Hinweis — der Besucher sah eine Lücke, der Betrieb erfuhr nichts. Der App-Modus
 * hatte für denselben Fall einen Platzhalter, zeigte ihn aber schon während des Ladens.
 *
 * Erledigt heißt hier ausdrücklich auch „gescheitert": Ein Ladefehler ist der Fall, für den der
 * Platzhalter existiert. Und ohne Kontext auf der Seite gilt es sofort — dann wird gar nicht erst
 * geladen, und darauf zu warten hieße, für immer zu warten.
 */
const settled = ref(false)

export const bundlesSettled = readonly(settled)

export function markBundlesSettled(): void {
  settled.value = true
}

/** Für Tests: zurück in den Zustand vor dem Laden. */
export function resetBundleReadiness(): void {
  settled.value = false
}
