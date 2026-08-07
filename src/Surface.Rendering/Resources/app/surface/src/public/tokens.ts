import baseTokens from '../styles/tokens.scss?inline'

/**
 * Die neutralen Basis-Tokens der Fläche, als CSS-Text.
 *
 * Für einen Host, der Flächen-Blöcke zeigt, ohne die Fläche zu sein. Auf einer Fläche lädt die
 * Runtime dieses Stylesheet selbst; in einem Editor-Canvas tut das niemand, und ein Block, der
 * `var(--cal-color-fg)` liest, fiele auf nichts zurück — die Vorschau sähe anders aus als das
 * Ergebnis, ohne dass etwas kaputt wirkt.
 *
 * Als Text und nicht als Datei, aus zwei Gründen: Der Konsument muss ihn scopen (eingebunden
 * gestalteten die `:root`-Regeln die Shell mit), und ein `?inline`-Import über eine
 * Paketgrenze hinweg zwänge ihm sass und einen Dateizugriff außerhalb seines Projekts auf.
 * Hier wird er beim Bauen des Pakets eingebettet, aus derselben `tokens.scss`, die die Fläche
 * lädt — es gibt keine zweite Liste, die auseinanderlaufen könnte.
 */
export const surfaceBaseTokens: string = baseTokens
