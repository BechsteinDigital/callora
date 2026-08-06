# Akzeptierte npm-Advisories

Jede aktive npm-Abhängigkeit soll frei von bekannten Advisories sein (Issue #121). Wo kein Fix
existiert, steht der Befund hier — mit Begründung und Ablaufdatum, nicht stillschweigend.

**Prüfung wiederholen:** `npm audit --package-lock-only` je Projekt. Ein Eintrag, dessen Ablauf
erreicht ist, wird neu bewertet oder behoben; er verlängert sich nicht von selbst.

---

## docs-site — vitepress, vite, esbuild

| | |
|---|---|
| **Eingetragen** | 2026-08-06 |
| **Erneut bewerten** | 2026-11-06 |
| **Betroffen** | `vitepress` (moderate, direkt), `vite` (high, transitiv), `esbuild` (moderate, transitiv) |
| **Advisories** | Vite Path Traversal in Optimized Deps `.map` Handling; launch-editor NTLMv2-Hash-Offenlegung über UNC-Pfade unter Windows; esbuild erlaubt jeder Website Anfragen an den Entwicklungsserver |

**Warum kein Fix.** Die Advisories sind in Vite 7.2.x behoben. VitePress 1.6.4 — die neueste
stabile Version — hängt an `vite ^5.4.14`. VitePress 2 existiert bisher nur als
`2.0.0-alpha.19`. Es gibt also keinen stabilen Pfad auf ein gefixtes Vite, ohne die Doku-Site
auf eine Alpha zu stellen.

**Warum vertretbar.** Alle drei Befunde betreffen den **Entwicklungsserver**, nicht das gebaute
Artefakt: `vite dev` und `esbuild serve` laufen nur lokal beim Schreiben der Dokumentation. Die
Doku wird gebaut und als statisches HTML ausgeliefert; im Auslieferungsstand läuft weder Vite
noch esbuild. Die Windows-UNC-Offenlegung ist zusätzlich plattformgebunden und trifft die
Linux-CI nicht.

**Was den Eintrag beendet.** Ein stabiles VitePress 2 (oder ein 1.x-Release auf Vite 7). Dann
aktualisieren und diesen Abschnitt löschen.

---

## Nicht geprüft

- `custom/plugins/*` — Beispiel- und Vorlagen-Plugins, werden entfernt.
- `custom/static-plugins/_archive/*` — archiviert, wird weder gebaut noch ausgeliefert.
