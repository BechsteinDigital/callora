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

## Hooks (eingreifen) + Service-Override (ersetzen)

Drei Erweiterungs-Stufen: (1) **Slots** = einfügen, (2) **Hooks** = eingreifen
(cancel/mutate), (3) **Service-Override** = ganze Services ersetzen. Statt Shopwares
freiem `Component.override` (fragil, koppelt an Interna) — kontrolliert über
explizite, benannte Verträge.

- `core/extensions/hooks.ts`: `registerHook(name, handler, order?)` + `runHook(name,
  payload)`. Handler laufen aufsteigend geordnet, sehen den **mutierbaren** `payload`
  und können via `ctx.cancel(reason)` **abbrechen** (erster Cancel gewinnt,
  kurzschließt die restlichen). Kern-Aktionen rufen `before`/`after`-Hooks.
- `core/extensions/services.ts`: `registerService(key, impl)` + `useService(key,
  fallback)`. Views resolven ihren Service darüber; ein Plugin ersetzt ihn. Nur
  explizit verdrahtete Services sind überschreibbar (kein Monkey-Patching).

Hook-Contracts (`{modul}.{before|after}-{action}`): `users.before-save`,
`users.after-save`, `users.before-delete`, `users.after-delete`; `roles.*` analog.
Service-Keys: `usersApi`, `rolesApi`.

## Neustart oder Refresh?

Ziel: **Browser-Refresh, kein Neustart** — wie Shopwares App-System. Die Mechanik
(Slots/Hooks/Services) läuft rein im Browser; Callora installiert/aktiviert Plugins
hot (Backend, ohne Neustart) und publiziert die UI-Assets zur Laufzeit. Sobald der
Micro-Frontend-Loader steht, lädt ein Refresh die neuen Bundles → `register*` läuft
→ Erweiterung sichtbar. Bis dahin würde Plugin-UI beim Admin-**Build** gebündelt.

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
