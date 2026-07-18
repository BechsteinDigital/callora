# Admin-Shell: UI-Extension-Slots (Design)

Datum: 2026-07-18 · Task #30. Erster Baustein der Plugin-UI-Erweiterbarkeit
(Extension-Landkarte Hebel D: „UI-Slots (Admin-Shell)"). Schließt die dort
notierte Lücke „nur deklarativ (Navigation/Routes), keine Render-Slots".

## Ziel

Plugins erweitern bestehende Core-Views additiv (Felder, Aktionen, Toolbar-
Buttons) — ohne die Views zu überschreiben. Vue-3-nativ statt Shopware-Component-
Override (das ist ein Vue-2/Twig-Erbe: fragiles Template-Override; wir koppeln
NICHT an interne View-Strukturen).

## Mechanik

- `core/extensions/registry.ts`: `registerExtension(slot, component, order?)` +
  `getExtensions(slot)`. Modul-level, statisch pro Session (Plugins registrieren
  beim Load). Ordnung aufsteigend, stabil bei Gleichstand.
- `core/extensions/ExtensionSlot.vue`: `<ExtensionSlot name :ctx>` rendert alle
  registrierten Komponenten des Slots und reicht `ctx` als Prop durch; leerer
  Slot rendert nichts.

## Slot-Contracts (öffentlich, stabil halten)

Konvention `{modul}.{view}.{position}`:

| Slot | View | ctx |
|------|------|-----|
| `users.list.toolbar` | UsersListView (Kopf) | — |
| `users.list.row-actions` | UsersListView (je Zeile) | `BackendUser` |
| `users.detail.fields` | UserDetailView (nach den Feldern) | `{ userId }` |
| `roles.list.toolbar` | RolesListView (Kopf) | — |
| `roles.detail.fields` | RoleDetailView (nach der Matrix) | `{ role }` |

User ist bewusst am reichhaltigsten (Liste **und** Detail), da dort der größte
Betreiber-Erweiterungsbedarf liegt (Profilfelder, Listen-Aktionen).

## Nicht-Ziele / später
- **Spalten-Injektion** in Listen (Header+Zelle mit Zeilenkontext) — komplexer,
  use-case-getrieben.
- **Micro-Frontend-Loading** (Plugin bringt gebautes JS, das `registerExtension`
  ruft; Nav-Einträge aus `/api/ext/admin/navigation`, Plugin-Views dynamisch) —
  eigener Baustein, wenn das erste Plugin eine Admin-UI mitbringt.
- SFC bleibt (keine Datei-Trennung SCSS/TS/Vue — die wäre nur für Override-Systeme
  nötig, die wir nicht bauen).

## Tests
`registry.test.ts` (Registrierung, Ordnung, Slot-Isolation);
`ExtensionSlot.test.ts` (leer, Reihenfolge, ctx-Durchreichung, Slot-Isolation).
Die bestehenden View-Tests bleiben grün (leere Slots rendern nichts).
