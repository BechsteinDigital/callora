# Surface-Unterbau (Stufe 1) — Implementierungs-Spec

Stand: 19. Juli 2026
Status: Bauplan. Design-Autorität = **ADR-014 (Surface-Engine)** §5/§7/§14/§15/§16. Dieses Spec ist
die konkrete Stufe-1-Ausführung; es erfindet keine Semantik, sondern schneidet die ADR auf Stufe 1 zu.
Verwandt: [[callora-tenant-workspace-surface-semantik]], ADR-015 (Surface-Template-Engine, Rendering —
bewusst SPÄTER).

## 1. Ziel & Zuschnitt

Eine **Surface ≈ Shopware-SalesChannel** (ADR-014 §18.1): konkrete Zugangs-/Ausgabefläche *innerhalb*
eines Workspaces; ein Workspace hat **N Surfaces auf geteilten Daten**. Heute ist das kollabiert — der
Workspace selbst trägt `PublicHost`/`PublicPathPrefix`/`PublicBaseUrl`/`Theme*` (1 Workspace = 1 Zugang).
Der Unterbau entfaltet das zu 1→N.

**Stufe-1-Schnitt (nach ADR §15 „Framework-Kern zuerst, Template-Compiler zuletzt"):**

| Phase (ADR) | Inhalt | Stufe 1 |
|---|---|---|
| A Surface-Domänenmodell | `WorkspaceSurface`-Entity, AccessMode, Domain-Auflösung, Surface-Scope | **JA** |
| C Surface-Administration | CRUD-API + Admin-UI | **JA** |
| D Surface-Runtime | `Host/Pfad → Surface → Workspace`, Access-Policy | **JA** |
| F/G/H/I SurfaceShell + Template-Bundles + Compiler | Multi-Inheritance-Rendering (Scriban, ADR-015) | **NEIN** — §15: SPA-Root genügt; vorhandene Shells laufen als je 1 Surface |
| E Identity/Principal-Profile | Employee/Agent/Customer, Audience/Realm-Tiefe | **NEIN** — bis Multi-Audience real gebraucht |

**Nicht-Ziele (Stufe 1):** Template-Compiler/SurfaceShell/Bundle-Mechanismus; volle Audience/
Auth-Realm-Maschinerie + Identity-Profile; Umbenennung `Callora.Workspace` → `Callora.Surface` (ADR §14,
später); Preview/Test-Modus für Surfaces.

## 2. Domänenmodell (Phase-A-Kern)

Neue Entity `WorkspaceSurface` (`src/Core/Domain/Workspaces/WorkspaceSurface.cs`), Felder aus ADR §5.2 —
Audience/Realm bewusst schmal:

| Feld | Typ | Herkunft/Zweck |
|---|---|---|
| `Id` | Guid | PK |
| `WorkspaceId` | Guid (FK) | Workspace-Zugehörigkeit |
| `SurfaceKey` | string | technischer Schlüssel, **unique je Workspace** |
| `DisplayName` | string | UI-Name |
| `SurfaceType` | string | erweiterbarer Schlüssel (ADR §16), Default `"spa"` — KEIN geschlossenes Enum |
| `PublicHost` | string? | Domain-Auflösung |
| `PublicPathPrefix` | string (`"/"`) | Entry-Route |
| `PublicBaseUrl` | string? | Parität zum Workspace heute |
| `AccessMode` | enum `Public`/`Authenticated`/`Mixed` (ADR §6.1) | Zugriffspolitik |
| `Locale` | string? | Sprache |
| `TemplatePluginId`/`TemplateVersion` | string? | Template-Zuweisung (SPA-Root-Default) |
| `ThemePluginId`/`ThemeVersion`/`ThemeAssignedBy`/`ThemeAssignedAtUtc` | string?/DateTimeOffset? | Theme-Zuweisung (aus Workspace übernommen) |
| `IsActive` | bool | Lifecycle |
| `CreatedAtUtc`/`UpdatedAtUtc` | DateTimeOffset | Audit |

EF-Config `WorkspaceSurfaceEntityTypeConfiguration` → Tabelle `workspace_surfaces`; FK auf `workspaces`
(`OnDelete: Cascade` — Surfaces sterben mit dem Workspace); Unique-Index `(WorkspaceId, SurfaceKey)`;
Index auf `PublicHost` für die Auflösung. `AccessMode` als string/enum-Konversion.
`Workspace` bekommt `ICollection<WorkspaceSurface> Surfaces`.

**Surface-Scope:** `BackendClaimTypes`/Scope-Mechanik um eine (optionale) Surface-Ebene ergänzen
(Platform→Tenant→Workspace→Surface, ADR §7) — für Stufe 1 nur die Achse anlegen, noch nicht flächig
erzwingen.

## 3. Migrationsstrategie (phasiert, non-breaking)

Kern-Entscheidung: **Surfaces additiv einführen, Autoritäts-Umschaltung erst mit der Runtime.** So bleibt
jeder Baustein reviewbar und bricht nichts.

- **S1 additiv:** `workspace_surfaces`-Tabelle + Entity + Store; EF-Migration `AddWorkspaceSurfaces`
  **backfillt** je Workspace eine `"default"`-Surface (kopiert `Public*`/`Theme*`, `SurfaceType="spa"`,
  `AccessMode=Mixed`, `IsActive=workspace.IsActive`) via `migrationBuilder.Sql(...)`. Der Workspace behält
  seine `Public*`/`Theme*`-Spalten unverändert; Runtime/Snapshot/Shell laufen wie bisher. **Nichts ändert
  Verhalten.**
- **S2 Autorität:** Runtime-Auflösung + Theme lesen aus Surfaces (Default-Surface bewahrt heutiges
  Verhalten). `WorkspacePublicRouteMatcher`/`ResolveByPublicRouteAsync`/Shell-Bootstrap → surface-basiert;
  Workspace-Upsert schreibt die Default-Surface durch (kein Drift).
- **S4 Cleanup:** `Workspace.Public*`/`Theme*`-Spalten entfernen (Consumer lesen dann aus Surfaces).

## 4. Bausteine

**S1 — Surface-Domänenmodell + Migration (additiv).**
- `WorkspaceSurface` Entity + `WorkspaceSurfaceEntityTypeConfiguration` + `Workspace.Surfaces`-Nav.
- `SurfaceAccessMode` Enum. `WorkspaceSurfaceSnapshot` (Read-Model).
- `IWorkspaceSurfaceStore` (List/Get/Upsert/Delete je Workspace) + `EfWorkspaceSurfaceStore`.
- EF-Migration `AddWorkspaceSurfaces` + Backfill-SQL (default-Surface je Workspace).
- Tests: Store-CRUD (Testcontainers, `[Trait Category=Slow]`), Snapshot-Mapping, Backfill-Unit falls
  isolierbar. PublicAPI-Baseline.
- **Rein additiv — keine bestehenden Consumer angefasst.**

**S2 — Surface-Runtime-Auflösung.**
- `WorkspacePublicRouteMatcher` → `SurfacePublicRouteMatcher` (Host/Pfad → Surface, Score wie heute);
  `ResolveByPublicRouteAsync` liefert Surface (+ zugehörigen Workspace).
- Shell-Bootstrap-Payload um `surface.key`/`surface.themePluginId` erweitern
  (`WorkspacePublicEndpoints`).
- Workspace-Upsert schreibt Default-Surface durch (Anti-Drift), bis S4 die Spalten entfernt.
- Tests: Auflösungs-Präzedenz, Default-Surface-Fallback, Bootstrap-Payload.

**S3 — Surface-Admin-API.**
- `SurfaceApiResponse`/`UpsertSurfaceApiRequest`; `SurfaceEndpoints` unter
  `/api/workspaces/{workspaceKey}/surfaces` (List/Upsert/Delete), permission-gated
  (`workspace.surfaces.manage`, ADR §3.5) + Workspace-Scope.
- Surface-Business-Events (`surface.created/updated/deleted`) analog EV1 (optional, konsistent).
- Tests: Endpoint-Integration (In-Memory-Store), Permission-Gating.

**S4 — Admin-Frontend `surfaces` + Lücken + Spalten-Cleanup.**
- Neues Admin-Modul `surfaces/` (List/Detail je Workspace) + Route + Nav-Link in `WorkspaceDetailView`.
- Übrige Frontend-Lücken (aus Bestandsaufnahme): **Entitlement-Verwaltung-UI**, **permission-gefilterte
  Navigation** (ADR §3.4/§16 Phase B), echtes **Dashboard**.
- Cleanup: `Workspace.Public*`/`Theme*`-Spalten entfernen (Migration), Consumer final auf Surfaces.
- Tests: Vitest (Views/API), .NET-Migration.

## 5. Entscheidungs-Log

| Entscheidung | Begründung |
|---|---|
| Stufe-1 = ADR-Phasen A+C+D, Rendering (F–I) + Identity-Profile (E) vertagt | ADR §15 eigene Reihenfolge; SPA-Root genügt, Template-Compiler erst bei 2. Oberflächentyp |
| Migration additiv (S1), Autorität erst mit Runtime (S2), Spalten-Drop erst S4 | jeder Baustein non-breaking + reviewbar; kein Big-Bang |
| `SurfaceType` als erweiterbarer String, nicht Enum | ADR §16 („nicht geschlossenes Enum") |
| Audience/Auth-Realm/Identity-Profile schmal/vertagt | ADR §15-Philosophie; für Ein-Verkäufer-Stufe-1 nicht nötig |
| Default-Surface je Workspace bei Migration | bewahrt heutiges 1-Zugang-Verhalten; 1→N ist additiv |
| Cascade-Delete Surface mit Workspace | Surface ist Sub-Resource des Workspaces (ADR §5.1) |

## 6. Offene Punkte

- Surface-Scope-Claim: nur Achse anlegen (Stufe 1) vs. flächig erzwingen (später, ADR §16 Phase B).
- `Callora.Workspace` → `Callora.Surface`-Umbenennung (ADR §14): vertagt, kein Stufe-1-Blocker.
- Ob S3 Surface-Business-Events mitnimmt (Konsistenz zu EV1–EV4) — Kann-Entscheidung im Baustein.
